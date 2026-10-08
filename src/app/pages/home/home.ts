import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { RouterLink } from '@angular/router';
import { Icon } from '../../shared/icon';
import {
  about, faqs, fill, finalCta, hero, pricing, problem, site, steps, subjects,
} from '../../content/site-content';

@Component({
  selector: 'app-home',
  imports: [RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './home.html',
  styleUrl: './home.scss',
})
export class Home {
  readonly site = site;
  readonly hero = hero;
  readonly problem = problem;
  readonly steps = steps;
  readonly subjects = subjects;
  readonly pricing = pricing;
  readonly about = about;
  readonly faqs = faqs;
  readonly finalCta = finalCta;
  readonly fill = fill;

  constructor() {
    inject(Title).setTitle('Engineered Tutors | Grade 12 Maths & Physical Sciences Online');
    inject(Meta).updateTag({
      name: 'description',
      content:
        'Online Grade 12 Maths and Physical Sciences coaching, taught by an engineer. Take the free diagnostic to find exactly where marks are being lost.',
    });
  }
}
