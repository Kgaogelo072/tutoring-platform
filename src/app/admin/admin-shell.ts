import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Title } from '@angular/platform-browser';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AdminAuth } from './admin-auth';

/** Admin layout: signs the tutor in, then shows the admin pages (AD-01). */
@Component({
  selector: 'app-admin-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="top">
      <div class="wrap row">
        <a routerLink="/admin" class="brand">Engineered Tutors <span>admin</span></a>
        @if (auth.signedIn()) {
          <nav class="row">
            <a routerLink="/admin" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="active">Leads</a>
            <a routerLink="/admin/questions" routerLinkActive="active">Questions</a>
          </nav>
          <span class="spacer"></span>
          <span class="user">{{ auth.userName() }}</span>
          <button type="button" class="link" (click)="auth.signOut()">Sign out</button>
        }
      </div>
    </header>

    <div class="wrap">
      @if (!auth.ready()) {
        <p class="muted pad">Loading…</p>
      } @else if (auth.signedIn()) {
        <router-outlet />
      } @else {
        <div class="card signin">
          <h1>Sign in</h1>
          @if (auth.mode === 'entra') {
            <p class="muted">Use the tutor's Microsoft account.</p>
            <button type="button" class="btn btn-navy" (click)="auth.signIn()">Sign in with Microsoft</button>
          } @else {
            <p class="muted">
              Local development: enter the API's <code>AdminAuth__DevKey</code> from <code>api/.env</code>.
              Production uses Microsoft sign-in.
            </p>
            <form (ngSubmit)="signIn()" class="row">
              <input type="password" name="key" [(ngModel)]="key" placeholder="Dev admin key" autocomplete="off" aria-label="Dev admin key" />
              <button type="submit" class="btn btn-navy" [disabled]="busy()">Sign in</button>
            </form>
          }
          @if (auth.error()) { <p class="alert">{{ auth.error() }}</p> }
        </div>
      }
    </div>
  `,
  styleUrl: './admin-shared.scss',
  styles: `
    :host { min-height: 100vh; background: #f4f6fa; padding-bottom: 48px; }
    .top { background: var(--navy); color: #fff; }
    .top .row { min-height: 60px; }
    .wrap { max-width: 1200px; margin: 0 auto; padding: 0 16px; }
    .brand { color: #fff; text-decoration: none; font-family: var(--font-display); font-size: 1.15rem; }
    .brand span { color: var(--gold-2); font-family: var(--font-body); font-size: 0.8rem; text-transform: uppercase; letter-spacing: 0.1em; }
    nav a { color: #cfd8ea; text-decoration: none; padding: 8px 10px; border-radius: 8px; font-weight: 600; }
    nav a.active { color: #fff; background: rgba(255, 255, 255, 0.12); }
    .user { color: #cfd8ea; font-size: 0.9rem; }
    .link { background: none; border: 0; color: #fff; text-decoration: underline; font: inherit; cursor: pointer; min-height: 44px; }
    .signin { max-width: 520px; margin: 48px auto; }
    .signin input { flex: 1; min-width: 200px; }
    .pad { padding: 32px 0; }
    code { background: #eef1f6; padding: 1px 6px; border-radius: 6px; }
  `,
})
export class AdminShell implements OnInit {
  protected readonly auth = inject(AdminAuth);
  protected readonly busy = signal(false);
  protected key = '';

  constructor() {
    inject(Title).setTitle('Admin | Engineered Tutors');
  }

  ngOnInit() {
    void this.auth.init();
  }

  protected async signIn() {
    this.busy.set(true);
    await this.auth.signIn(this.key);
    this.busy.set(false);
  }
}
