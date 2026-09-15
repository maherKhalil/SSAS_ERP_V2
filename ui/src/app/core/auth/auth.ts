import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class Auth {
  private _isAuthenticated = true;

  login(): void {
    this._isAuthenticated = true;
  }

  logout(): void {
    this._isAuthenticated = false;
  }

  isAuthenticated(): boolean {
    return this._isAuthenticated;
  }

  getCurrentUser(): { username: string } | null {
    return this._isAuthenticated ? { username: 'Admin' } : null;
  }

  getToken(): string | null {
    return localStorage.getItem('token');
  }
}
