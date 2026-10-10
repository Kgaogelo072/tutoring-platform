import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { IdType, Subject, apiUrl } from '../../core/api';

export interface RegistrationOptions {
  sources: string[];
  goals: string[];
  subjects: { subject: Subject; name: string; available: boolean; minutes: number }[];
  maxLearners: number;
  consentTextVersion: string;
}

export interface RegistrationRequest {
  parent: { fullName: string; phone: string; email: string; source: string };
  learners: {
    firstName: string;
    surname: string;
    school: string;
    grade: number | null;
    subjects: { subject: Subject; latestMark: number | null }[];
    goal: string;
    idType: IdType | null;
    idNumber: string | null;
  }[];
  isGuardianOver18: boolean;
  consentAccepted: boolean;
  consentTextVersion: string;
  turnstileToken: string | null;
  website: string;
}

export interface RegistrationResponse {
  message: string;
  links: { firstName: string; tests: { subject: Subject; url: string; minutes: number }[] }[];
}

@Injectable({ providedIn: 'root' })
export class RegistrationApi {
  private readonly http = inject(HttpClient);

  options() {
    return this.http.get<RegistrationOptions>(apiUrl('/registrations/options'));
  }

  register(body: RegistrationRequest) {
    return this.http.post<RegistrationResponse>(apiUrl('/registrations'), body);
  }
}
