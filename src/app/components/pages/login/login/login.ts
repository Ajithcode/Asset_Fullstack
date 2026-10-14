import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgIf } from '@angular/common';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { AuthService } from '../../../../components/service/auth/auth.service';
import { LoginData } from '../../../../models/auth.model';

/** DEMO MODE: any username + password signs in straight to the dashboard (no backend, no OTP). */
@Component({
  standalone: true,
  selector: 'app-login',
  imports: [FormsModule, NgIf, RouterLink],
  templateUrl: './login.html',
  styleUrls: ['./login.css']
})
export class Login {
  credentials: LoginData = { userName: '', password: '' };
  errorMsg = '';
  infoMsg = '';
  isLoading = false;

  constructor(private auth: AuthService, private router: Router, private route: ActivatedRoute) {
    // Arriving from sign-up: prefill the new username
    const registered = this.route.snapshot.queryParamMap.get('registered');
    if (registered) {
      this.credentials.userName = registered;
      this.infoMsg = 'Account created. Sign in to continue.';
    }
  }

  login(): void {
    const userName = this.credentials.userName.trim();
    if (!userName || !this.credentials.password) {
      this.errorMsg = 'Enter your username and password.';
      return;
    }

    this.errorMsg = '';
    this.infoMsg = '';
    this.auth.login({ userName, password: this.credentials.password });
    this.credentials.password = '';
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    this.router.navigateByUrl(returnUrl || '/dashboard');
  }
}
