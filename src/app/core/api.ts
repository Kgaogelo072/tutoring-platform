import { HttpErrorResponse } from '@angular/common/http';
import { appSettings } from '../content/app-settings';

export const apiUrl = (path: string) => `${appSettings.apiBaseUrl}/api${path}`;

export type Subject = 'Maths' | 'PhysicalSciences';
export type IdType = 'SA_ID' | 'PASSPORT';
export type QuestionType = 'MCQ' | 'NUM';
export type AttemptStatus = 'NotStarted' | 'InProgress' | 'Submitted';
export type LeadStatus = 'New' | 'Tested' | 'ReportSent' | 'CallBooked' | 'Registered' | 'NotInterested';
export type Band = 'Strong' | 'Developing' | 'PriorityGap';

export const subjectNames: Record<Subject, string> = {
  Maths: 'Mathematics',
  PhysicalSciences: 'Physical Sciences',
};

export const leadStatusNames: Record<LeadStatus, string> = {
  New: 'New',
  Tested: 'Tested',
  ReportSent: 'Report sent',
  CallBooked: 'Call booked',
  Registered: 'Registered',
  NotInterested: 'Not interested',
};

export const bandNames: Record<Band, string> = {
  Strong: 'Strong',
  Developing: 'Developing',
  PriorityGap: 'Priority gap',
};

/** Field errors keyed by JSON path, e.g. "learners[0].idNumber" (ASP.NET ValidationProblem). */
export type FieldErrors = Record<string, string[]>;

export interface ApiError {
  status: number;
  code?: string;
  message: string;
  fieldErrors?: FieldErrors;
}

/** Turns an HttpClient error into something a page can show. */
export function toApiError(err: unknown): ApiError {
  if (err instanceof HttpErrorResponse) {
    const body = err.error && typeof err.error === 'object' ? err.error : {};
    if (err.status === 0) {
      return { status: 0, code: 'offline', message: "We couldn't reach the server. Check your connection and try again." };
    }
    return {
      status: err.status,
      code: body.code,
      message:
        body.detail ??
        body.title ??
        (err.status === 429 ? 'Too many attempts. Please wait a while and try again.' : 'Something went wrong. Please try again.'),
      fieldErrors: body.errors && !Array.isArray(body.errors) ? body.errors : undefined,
    };
  }
  return { status: -1, message: 'Something went wrong. Please try again.' };
}

/** SAST is UTC+2 all year (NF-07). */
export function sast(utc: string | null | undefined, withTime = true): string {
  if (!utc) return '';
  const d = new Date(utc.endsWith('Z') || utc.includes('+') ? utc : utc + 'Z');
  return new Intl.DateTimeFormat('en-ZA', {
    timeZone: 'Africa/Johannesburg',
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    ...(withTime ? { hour: '2-digit', minute: '2-digit', hour12: false } : {}),
  }).format(d);
}
