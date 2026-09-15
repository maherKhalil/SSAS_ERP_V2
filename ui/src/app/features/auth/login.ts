import { Component, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Auth } from '../../core/auth/auth';

@Component({
  selector: 'app-login',
  standalone: true,
  template: `
    <div class="min-h-screen flex items-center justify-center bg-gray-100">
      <div class="bg-white p-8 rounded-lg shadow-md w-96 text-center">
        <h2 class="text-2xl font-bold mb-6 text-gray-800">ERP Login</h2>
        <p class="text-sm text-gray-600 mb-6">Enter your credentials to access the system.</p>
        <button (click)="doLogin()" class="w-full bg-blue-600 text-white font-semibold py-2 px-4 rounded hover:bg-blue-700 transition">
          Sign In
        </button>
      </div>
    </div>
  `
})
export class LoginComponent {
  private auth = inject(Auth);
  private router = inject(Router);

  doLogin() {
    this.auth.login();
    this.router.navigate(['/modules']);
  }
}
