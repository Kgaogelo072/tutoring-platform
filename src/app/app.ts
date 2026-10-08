import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { SiteHeader } from './shared/site-header';
import { SiteFooter } from './shared/site-footer';
import { WhatsappButton } from './shared/whatsapp-button';

@Component({
  selector: 'app-root',
  imports: [RouterOutlet, SiteHeader, SiteFooter, WhatsappButton],
  template: `
    <app-site-header />
    <main><router-outlet /></main>
    <app-site-footer />
    <app-whatsapp-button />
  `,
})
export class App {}
