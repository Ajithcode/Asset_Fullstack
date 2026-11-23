import { Component, HostListener, OnInit } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ArrowRight, Box, ChartColumn, Eye, EyeOff, Lock, LucideAngularModule, ShieldCheck, Wrench } from 'lucide-angular';
import { environment } from 'shared-ui';

const MIN_PASSWORD = 8;
const DESIGN_H = 940;
const DESIGN_MIN_W = 1280;

/**
 * Invitation link (/accept-invite?token=...): the invited person chooses a password; the authenticator app is set up
 * at their first sign-in. POST /api/invitations/validate and /accept are public; the one-time token secures them.
 */
@Component({
  standalone: true,
  selector: 'app-accept-invite',
  imports: [FormsModule, NgIf, NgFor, RouterLink, LucideAngularModule],
  templateUrl: './accept-invite.html',
  styleUrls: ['../login/login/login.css', '../signup/signup/signup.css']
})
export class AcceptInvite implements OnInit {
  readonly icons = { ArrowRight, Eye, EyeOff, Lock };
  readonly features = [
    { label: 'Asset\nVisibility', icon: Box, tone: 'purple' },
    { label: 'Maintenance\nManagement', icon: Wrench, tone: 'teal' },
    { label: 'Operational\nEfficiency', icon: ChartColumn, tone: 'blue' },
    { label: 'Compliance\n& Governance', icon: ShieldCheck, tone: 'green' }
  ];
  readonly year = new Date().getFullYear();

  private token = '';
  checking = true;
  invite: { name: string; email: string; username: string } | null = null;
  errorMsg = '';
  password = '';
  confirmPassword = '';
  showPassword = false;
  showConfirm = false;
  isLoading = false;
  scaled = false;
  scale = 1;

  private readonly api = `${environment.administrationApiUrl}api/invitations`;

  constructor(private http: HttpClient, private route: ActivatedRoute, private router: Router) {
    this.fit();
  }

  @HostListener('window:resize')
  fit(): void {
    if (typeof window === 'undefined') return;
    const w = window.innerWidth;
    const h = window.innerHeight;
    this.scaled = w >= 1000 && w >= h;
    this.scale = this.scaled ? Math.min(h / DESIGN_H, w / DESIGN_MIN_W) : 1;
  }

  ngOnInit(): void {
    this.token = this.route.snapshot.queryParamMap.get('token') ?? '';
    if (typeof window === 'undefined') return;
    if (!this.token) return this.fail('This invitation link is incomplete. Open the link from your invitation exactly as sent.');
    this.http.post<{ name: string; email: string; username: string }>(`${this.api}/validate`, { token: this.token }).subscribe({
      next: (invite) => {
        this.invite = invite;
        this.checking = false;
      },
      error: (err) => this.fail(this.message(err, 'This invitation link could not be checked.'))
    });
  }

  private fail(message: string): void {
    this.errorMsg = message;
    this.checking = false;
  }

  private message(err: unknown, fallback: string): string {
    if (err instanceof HttpErrorResponse) {
      if (typeof err.error?.message === 'string') return err.error.message;
      if (err.status === 0) return 'The server could not be reached. Try again in a moment.';
    }
    return fallback;
  }

  accept(): void {
    if (this.isLoading || !this.invite) return;
    if (this.password.length < MIN_PASSWORD) return void (this.errorMsg = `Password must be at least ${MIN_PASSWORD} characters.`);
    if (this.password !== this.confirmPassword) return void (this.errorMsg = 'Passwords do not match.');
    this.errorMsg = '';
    this.isLoading = true;
    this.http.post<{ username: string }>(`${this.api}/accept`, { token: this.token, password: this.password }).subscribe({
      next: (res) => {
        this.isLoading = false;
        // Sign in with the new password; the authenticator app is set up on that first sign-in
        this.router.navigate(['/login'], { queryParams: { registered: res.username } });
      },
      error: (err) => {
        this.isLoading = false;
        this.errorMsg = this.message(err, 'Could not set your password.');
      }
    });
  }
}
