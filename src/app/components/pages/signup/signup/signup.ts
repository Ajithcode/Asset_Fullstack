import { Component } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgFor, NgIf } from '@angular/common';
import { Router, RouterLink } from '@angular/router';
import { AuthService, DEMO_ROLES } from '../../../../components/service/auth/auth.service';
import { RegisterData, SignupRole } from '../../../../models/auth.model';

const USER_NAME_PATTERN = /^[A-Za-z0-9._-]{3,50}$/;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const MIN_PASSWORD = 8;

/** DEMO MODE: sign-up is validated and kept in the browser only; afterwards the user signs in. */
@Component({
  standalone: true,
  selector: 'app-signup',
  imports: [FormsModule, NgIf, NgFor, RouterLink],
  templateUrl: './signup.html',
  styleUrls: ['../../login/login/login.css']
})
export class Signup {
  form: RegisterData = { fullName: '', userName: '', email: '', contactNo: '', password: '', userRoleId: '' };
  confirmPassword = '';
  errorMsg = '';
  isLoading = false;

  roles: SignupRole[] = DEMO_ROLES;
  rolesLoading = false;
  rolesError = '';

  constructor(private auth: AuthService, private router: Router) {}

  get selectedRole(): SignupRole | undefined {
    return this.roles.find((r) => r.roleId === this.form.userRoleId);
  }

  loadRoles(): void {
    this.roles = DEMO_ROLES;
  }

  private validate(): string | null {
    const f = this.form;
    if (!f.fullName.trim() || !f.userName.trim() || !f.email.trim() || !f.password) return 'Please fill in all required fields.';
    if (!USER_NAME_PATTERN.test(f.userName.trim())) return 'Username must be 3-50 characters: letters, numbers, dot, dash or underscore.';
    if (!EMAIL_PATTERN.test(f.email.trim())) return 'Enter a valid email address.';
    if (f.password.length < MIN_PASSWORD) return `Password must be at least ${MIN_PASSWORD} characters.`;
    if (f.password !== this.confirmPassword) return 'Passwords do not match.';
    if (!f.userRoleId) return 'Select a role.';
    return null;
  }

  signup(): void {
    this.errorMsg = this.validate() ?? '';
    if (this.errorMsg) return;

    const userName = this.form.userName.trim();
    this.auth.register({
      fullName: this.form.fullName.trim(),
      userName,
      email: this.form.email.trim(),
      contactNo: this.form.contactNo.trim(),
      password: this.form.password,
      userRoleId: this.form.userRoleId,
    });
    this.router.navigate(['/login'], { queryParams: { registered: userName } });
  }
}
