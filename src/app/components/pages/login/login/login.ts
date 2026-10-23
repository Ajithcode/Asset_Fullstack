import { Component, HostListener } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgFor, NgIf } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import {
  ArrowRight,
  Box,
  Building2,
  ChartColumn,
  ChevronDown,
  ChevronUp,
  Eye,
  EyeOff,
  KeyRound,
  Lock,
  LucideAngularModule,
  Mail,
  QrCode,
  Server,
  ShieldCheck,
  User,
  Wrench,
} from 'lucide-angular';
import { AuthService } from '../../../../components/service/auth/auth.service';
import { LoginData } from '../../../../models/auth.model';

const REMEMBER_KEY = 'a360.rememberedUser';
/** Design canvas of the login page (the mock is 1536 × 1024, drawn here with a tighter vertical rhythm). */
const DESIGN_H = 940;
const DESIGN_MIN_W = 1280;

type Directory = '' | 'Active Directory' | 'LDAP';

/** DEMO MODE: any username + password signs in straight to the dashboard (no backend, no OTP). */
@Component({
  standalone: true,
  selector: 'app-login',
  imports: [FormsModule, NgIf, NgFor, LucideAngularModule],
  templateUrl: './login.html',
  styleUrls: ['./login.css']
})
export class Login {
  readonly icons = { ArrowRight, Building2, ChevronDown, ChevronUp, Eye, EyeOff, KeyRound, Lock, Mail, QrCode, Server, User };
  readonly features = [
    { label: 'Asset\nVisibility', icon: Box, tone: 'purple' },
    { label: 'Maintenance\nManagement', icon: Wrench, tone: 'teal' },
    { label: 'Operational\nEfficiency', icon: ChartColumn, tone: 'blue' },
    { label: 'Compliance\n& Governance', icon: ShieldCheck, tone: 'green' },
  ];
  readonly year = new Date().getFullYear();

  credentials: LoginData = { userName: '', password: '' };
  errorMsg = '';
  infoMsg = '';
  isLoading = false;
  showPassword = false;
  rememberMe = true;
  otherOpen = true;
  /**
   * Desktop / laptop: the page is laid out at the design size (1024 units tall, at least 1280 wide) and scaled as a
   * whole to fit the window, so it keeps the design's proportions at any resolution or browser zoom.
   * Phones and portrait tablets use the one-column layout instead.
   */
  scaled = false;
  scale = 1;
  /** Set when the user picks Active Directory / LDAP: same form, signs in against that directory. */
  directory: Directory = '';

  constructor(private auth: AuthService, private router: Router, private route: ActivatedRoute) {
    this.fit();
    try {
      this.credentials.userName = localStorage.getItem(REMEMBER_KEY) ?? '';
    } catch {}
    // Arriving from sign-up: prefill the new username
    const registered = this.route.snapshot.queryParamMap.get('registered');
    if (registered) {
      this.credentials.userName = registered;
      this.infoMsg = 'Account created. Sign in to continue.';
    }
  }

  @HostListener('window:resize')
  fit(): void {
    if (typeof window === 'undefined') return;
    const w = window.innerWidth;
    const h = window.innerHeight;
    this.scaled = w >= 1000 && w >= h;
    this.scale = this.scaled ? Math.min(h / DESIGN_H, w / DESIGN_MIN_W) : 1;
  }

  login(): void {
    const userName = this.credentials.userName.trim();
    if (!userName || !this.credentials.password) {
      this.errorMsg = this.directory ? `Enter your ${this.directory} username and password.` : 'Enter your email or username and password.';
      return;
    }

    this.errorMsg = '';
    this.infoMsg = '';
    try {
      if (this.rememberMe) localStorage.setItem(REMEMBER_KEY, userName);
      else localStorage.removeItem(REMEMBER_KEY);
    } catch {}
    this.auth.login({ userName, password: this.credentials.password });
    this.credentials.password = '';
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    this.router.navigateByUrl(returnUrl || '/dashboard');
  }

  useDirectory(dir: Directory): void {
    this.directory = this.directory === dir ? '' : dir;
    this.errorMsg = '';
    this.infoMsg = this.directory ? `Sign in with your ${this.directory} account (for example DOMAIN\\username).` : '';
  }

  /** External identity providers need a backend; the demo build says so instead of pretending to sign in. */
  provider(name: string): void {
    this.errorMsg = '';
    this.infoMsg = `${name} isn't connected in this demo. Sign in with your username and password.`;
  }

  forgot(event: Event): void {
    event.preventDefault();
    this.errorMsg = '';
    this.infoMsg = 'Contact your administrator to reset your password.';
  }
}
