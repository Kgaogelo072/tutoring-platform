import { Component, computed, inject } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { filter, map } from 'rxjs';
import { SiteHeader } from './shared/site-header';
import { SiteFooter } from './shared/site-footer';
import { WhatsappButton } from './shared/whatsapp-button';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, SiteHeader, SiteFooter, WhatsappButton],
  template: `
    @if (bare()) {
      <main><router-outlet /></main>
    } @else {
      <app-site-header />
      <main><router-outlet /></main>
      <app-site-footer />
      <app-whatsapp-button />
    }
  `,
})
export class App {
  private readonly router = inject(Router);
  private readonly url = toSignal(
    this.router.events.pipe(
      filter((e) => e instanceof NavigationEnd),
      map((e) => (e as NavigationEnd).urlAfterRedirects),
    ),
    { initialValue: this.router.url },
  );

  /** The test page and admin area drop the marketing header, footer and WhatsApp button. */
  protected readonly bare = computed(() => /^\/(d|admin)(\/|$|\?)/.test(this.url()));
}
