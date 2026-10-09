import { ChangeDetectionStrategy, Component, DestroyRef, OnInit, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Subject as RxSubject, debounceTime, switchMap } from 'rxjs';
import { LeadStatus, leadStatusNames, sast, subjectNames, toApiError } from '../core/api';
import { AdminApi, Lead, whatsappToParent } from './admin-api';

/** AD-02 leads list with search, filter and sort; AD-05 contact shortcuts; AD-08 export. */
@Component({
  selector: 'app-leads-page',
  imports: [FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="row head">
      <h1>Leads</h1>
      <span class="spacer"></span>
      <button type="button" class="btn btn-outline" (click)="export()" [disabled]="exporting()">
        {{ exporting() ? 'Exporting…' : 'Export CSV' }}
      </button>
    </div>

    <div class="card row filters">
      <input type="search" [(ngModel)]="search" (ngModelChange)="reload()" placeholder="Search learner name, parent phone or ID number"
             aria-label="Search" class="grow" />
      <select [(ngModel)]="status" (ngModelChange)="reload()" aria-label="Lead status">
        <option value="">All statuses</option>
        @for (s of statuses; track s) { <option [value]="s">{{ statusNames[s] }}</option> }
      </select>
      <select [(ngModel)]="sort" (ngModelChange)="reload()" aria-label="Sort by">
        <option value="registered:desc">Newest first</option>
        <option value="registered:asc">Oldest first</option>
        <option value="name:asc">Parent name</option>
        <option value="score:desc">Highest score</option>
        <option value="score:asc">Lowest score</option>
        <option value="status:asc">Status</option>
      </select>
    </div>

    @if (error()) { <p class="alert">{{ error() }}</p> }
    @if (loading() && !leads().length) { <p class="muted">Loading…</p> }
    @if (!loading() && !leads().length && !error()) { <p class="muted">No leads match.</p> }

    @for (l of leads(); track l.id) {
      <article class="card lead">
        <div class="row">
          <div class="grow">
            <strong class="parent">{{ l.parentName }}</strong>
            <div class="muted small">{{ l.phone }} · {{ l.email }} · {{ l.source }} · registered {{ sast(l.registeredAt) }}</div>
          </div>
          <select [ngModel]="l.status" (ngModelChange)="setStatus(l, $event)" aria-label="Lead status" class="status">
            @for (s of statuses; track s) { <option [value]="s">{{ statusNames[s] }}</option> }
          </select>
        </div>

        <ul class="learners">
          @for (x of l.learners; track x.id) {
            <li>
              <a [routerLink]="['/admin/learners', x.id]">{{ x.name }}</a>
              <span class="muted small">Gr {{ x.grade }} · {{ x.school }}{{ x.idMasked ? ' · ID ' + x.idMasked : '' }}</span>
              <span class="tests">
                @for (t of x.tests; track t.subject) {
                  <span class="chip" [class]="'chip ' + t.status">
                    {{ subjectNames[t.subject] }}: {{ t.status === 'Submitted' ? t.overallPct + '%' : t.status === 'InProgress' ? 'in progress' : 'not started' }}
                  </span>
                }
              </span>
            </li>
          }
        </ul>

        <div class="row actions">
          <button type="button" class="btn btn-outline btn-sm" (click)="copy(l.phone)">{{ copied() === l.phone ? 'Copied ✓' : 'Copy phone' }}</button>
          <a class="btn btn-wa btn-sm" [href]="wa(l)" target="_blank" rel="noopener">WhatsApp</a>
          @if (l.notes) { <span class="muted small note">📝 {{ l.notes }}</span> }
        </div>
      </article>
    }
  `,
  styleUrl: './admin-shared.scss',
  styles: `
    .head { margin-top: 24px; }
    .filters .grow { flex: 1 1 260px; }
    .grow { flex: 1; min-width: 0; }
    .parent { font-size: 1.05rem; color: var(--navy); }
    .status { min-width: 150px; }
    .learners { list-style: none; padding: 0; margin: 12px 0; display: grid; gap: 8px; }
    .learners li { display: flex; flex-wrap: wrap; gap: 6px 12px; align-items: baseline; }
    .tests { display: inline-flex; gap: 6px; flex-wrap: wrap; }
    .note { flex-basis: 100%; white-space: pre-wrap; }
  `,
})
export class LeadsPage implements OnInit {
  private readonly api = inject(AdminApi);
  private readonly destroyRef = inject(DestroyRef);
  private readonly trigger = new RxSubject<void>();

  protected readonly statuses = Object.keys(leadStatusNames) as LeadStatus[];
  protected readonly statusNames = leadStatusNames;
  protected readonly subjectNames = subjectNames;
  protected readonly sast = sast;

  protected readonly leads = signal<Lead[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly exporting = signal(false);
  protected readonly copied = signal<string | null>(null);

  protected search = '';
  protected status: LeadStatus | '' = '';
  protected sort = 'registered:desc';

  ngOnInit() {
    this.trigger
      .pipe(
        debounceTime(250),
        switchMap(() => {
          this.loading.set(true);
          const [sort, dir] = this.sort.split(':') as [string, 'asc' | 'desc'];
          return this.api.leads({ search: this.search.trim(), status: this.status, sort, dir });
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (l) => {
          this.leads.set(l);
          this.loading.set(false);
          this.error.set(null);
        },
        error: (e) => {
          this.loading.set(false);
          this.error.set(toApiError(e).message);
        },
      });
    this.reload();
  }

  protected reload() {
    this.trigger.next();
  }

  protected setStatus(l: Lead, status: LeadStatus) {
    this.api.updateLead(l.id, { status }).subscribe({
      next: (r) => this.leads.update((all) => all.map((x) => (x.id === l.id ? { ...x, status: r.status } : x))),
      error: (e) => this.error.set(toApiError(e).message),
    });
  }

  protected wa(l: Lead) {
    return whatsappToParent(l.phone, l.parentName, l.learners[0]?.name.split(' ')[0]);
  }

  protected async copy(phone: string) {
    try {
      await navigator.clipboard.writeText(phone);
      this.copied.set(phone);
      setTimeout(() => this.copied.set(null), 2000);
    } catch {
      window.prompt('Copy the number:', phone);
    }
  }

  protected export() {
    this.exporting.set(true);
    this.api.exportLeads().subscribe({
      next: (res) => {
        const name = /filename="?([^";]+)/.exec(res.headers.get('content-disposition') ?? '')?.[1] ?? 'leads.csv';
        const url = URL.createObjectURL(res.body!);
        const a = document.createElement('a');
        a.href = url;
        a.download = name;
        a.click();
        URL.revokeObjectURL(url);
        this.exporting.set(false);
      },
      error: (e) => {
        this.exporting.set(false);
        this.error.set(toApiError(e).message);
      },
    });
  }
}
