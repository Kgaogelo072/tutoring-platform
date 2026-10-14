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
      <div class="wrap bar">
        <a routerLink="/admin" class="brand">
          <img src="img/logo-480.webp" srcset="img/logo-480.webp 480w, img/logo-960.webp 960w"
               sizes="200px" width="200" height="63" alt="Engineered Tutors" />
          <span class="tag">Admin</span>
        </a>
        @if (auth.signedIn()) {
          <nav class="nav" aria-label="Admin">
            <a routerLink="/admin" [routerLinkActiveOptions]="{ exact: true }" routerLinkActive="active">Leads</a>
            <a routerLink="/admin/questions" routerLinkActive="active">Questions</a>
          </nav>
          <div class="account">
            <span class="user">{{ auth.userName() }}</span>
            <button type="button" class="btn btn-outline btn-sm" (click)="auth.signOut()">Sign out</button>
          </div>
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
    :host { min-height: 100vh; background: #f4f6fa; padding-bottom: 48px; overflow-x: clip; }
    /* Matches the public site header (shared/site-header.ts). */
    .top {
      position: sticky; top: 0; z-index: 50;
      background: rgba(255, 255, 255, 0.94);
      backdrop-filter: blur(8px);
      border-bottom: 1px solid var(--line);
    }
    .wrap { max-width: var(--container); margin: 0 auto; padding: 0 var(--gutter); }
    .bar { display: flex; flex-wrap: wrap; align-items: center; gap: 8px 24px; min-height: 72px; padding-block: 8px; }
    .brand { display: inline-flex; align-items: center; gap: 10px; text-decoration: none; }
    .brand img { width: 150px; height: auto; }
    .tag {
      padding: 3px 10px; border-radius: 999px; background: var(--gold-soft); color: var(--navy);
      font-size: 0.72rem; font-weight: 700; text-transform: uppercase; letter-spacing: 0.1em;
    }
    .nav { display: flex; gap: 22px; order: 3; width: 100%; }
    .nav a {
      color: var(--navy); text-decoration: none; font-weight: 600; font-size: 0.95rem;
      padding: 8px 0; border-bottom: 2px solid transparent;
    }
    .nav a:hover { color: var(--gold); }
    .nav a.active { border-bottom-color: var(--gold); }
    .account { display: flex; align-items: center; gap: 14px; margin-left: auto; }
    .user { color: var(--muted); font-size: 0.9rem; }

    @media (min-width: 760px) {
      .brand img { width: 200px; }
      .nav { order: 0; width: auto; margin-left: 12px; }
    }
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
