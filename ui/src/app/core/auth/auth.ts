import { Service } from '@angular/core';

@Service()
export class Auth {
  private _isAuthenticated = false;

  login(): void {
    this._isAuthenticated = true;
  }

  logout(): void {
    this._isAuthenticated = false;
  }

  isAuthenticated(): boolean {
    // Mock state
    return this._isAuthenticated;
  }
}
