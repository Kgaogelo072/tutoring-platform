import { ChangeDetectionStrategy, Component, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { Icon } from './icon';

@Component({
  selector: 'app-site-header',
  imports: [RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <header class="header">
      <div class="container bar">
        <a routerLink="/" class="logo" (click)="open.set(false)">
          <img src="img/logo-480.webp" srcset="img/logo-480.webp 480w, img/logo-960.webp 960w"
               sizes="200px" width="200" height="63" alt="Engineered Tutors — Grade 12 Maths & Physical Sciences" />
        </a>

        <nav class="nav" [class.open]="open()" aria-label="Main">
          @for (link of links; track link.fragment) {
            <a routerLink="/" [fragment]="link.fragment" (click)="open.set(false)">{{ link.label }}</a>
          }
          <a routerLink="/diagnostic" class="btn btn-gold nav-cta" (click)="open.set(false)">
            Free diagnostic <app-icon name="arrow" [size]="18" />
          </a>
        </nav>

        <button class="menu" type="button" (click)="open.set(!open())"
                [attr.aria-expanded]="open()" aria-label="Toggle menu">
          <app-icon [name]="open() ? 'close' : 'menu'" [size]="26" />
        </button>
      </div>
    </header>
  `,
  styles: `
    .header {
      position: sticky; top: 0; z-index: 50;
      background: rgba(255, 255, 255, 0.94);
      backdrop-filter: blur(8px);
      border-bottom: 1px solid var(--line);
    }
    .bar { display: flex; align-items: center; justify-content: space-between; min-height: 72px; gap: 16px; }
    .logo img { width: 170px; height: auto; }
    .nav { display: none; }
    .menu {
      display: inline-flex; align-items: center; justify-content: center;
      width: 48px; height: 48px; border: 0; background: none; color: var(--navy); cursor: pointer;
    }
    .nav.open {
      display: flex; flex-direction: column; gap: 4px;
      position: absolute; left: 0; right: 0; top: 72px;
      background: #fff; padding: 12px var(--gutter) 20px; border-bottom: 1px solid var(--line);
      box-shadow: var(--shadow);
    }
    .nav a:not(.btn) {
      text-decoration: none; font-weight: 600; color: var(--navy);
      padding: 12px 4px; font-size: 1rem;
    }
    .nav a:not(.btn):hover { color: var(--gold); }
    .nav-cta { margin-top: 8px; }

    @media (min-width: 960px) {
      .logo img { width: 200px; }
      .menu { display: none; }
      .nav, .nav.open {
        display: flex; flex-direction: row; align-items: center; gap: 26px;
        position: static; padding: 0; box-shadow: none; border: 0; background: none;
      }
      .nav a:not(.btn) { padding: 8px 0; font-size: 0.95rem; }
      .nav-cta { margin: 0 0 0 6px; min-height: 44px; }
    }
  `,
})
export class SiteHeader {
  open = signal(false);
  links = [
    { label: 'How it works', fragment: 'how-it-works' },
    { label: 'Subjects', fragment: 'subjects' },
    { label: 'Pricing', fragment: 'pricing' },
    { label: 'About', fragment: 'about' },
    { label: 'FAQ', fragment: 'faq' },
  ];
}
