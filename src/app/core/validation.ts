import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Client-side copies of the API's checks, so parents see problems before submitting. The API is the authority and
 * repeats every check (REG-01, REG-09, DX-11).
 */

/** SA mobile number → +27XXXXXXXXX, or null. Mirrors Normalise.SaMobile. */
export function saMobile(raw: string | null | undefined): string | null {
  if (!raw?.trim()) return null;
  const digits = raw.replace(/\D/g, '');
  const trimmed = raw.trim();
  let national: string;
  if (trimmed.startsWith('+')) {
    if (!digits.startsWith('27')) return null;
    national = digits.slice(2);
  } else if (digits.startsWith('27') && digits.length === 11) national = digits.slice(2);
  else if (digits.startsWith('0') && digits.length === 10) national = digits.slice(1);
  else return null;
  if (national.length !== 9 || !'678'.includes(national[0])) return null;
  return '+27' + national;
}

export const normaliseId = (raw: string) => raw.replace(/[\s-]/g, '').toUpperCase();

function luhn(digits: string): boolean {
  let sum = 0;
  let dbl = false;
  for (let i = digits.length - 1; i >= 0; i--) {
    let d = digits.charCodeAt(i) - 48;
    if (dbl) {
      d *= 2;
      if (d > 9) d -= 9;
    }
    sum += d;
    dbl = !dbl;
  }
  return sum % 10 === 0;
}

/** Returns an error message, or null if valid. Mirrors IdNumbers.Validate. */
export function idNumberError(type: 'SA_ID' | 'PASSPORT', raw: string, today = new Date()): string | null {
  const id = normaliseId(raw);
  if (type === 'PASSPORT') {
    return /^[A-Z0-9]{6,20}$/.test(id) ? null : 'A passport number has 6 to 20 letters and digits.';
  }
  if (!/^\d{13}$/.test(id)) return 'An SA ID number has 13 digits.';
  const yy = +id.slice(0, 2);
  const mm = +id.slice(2, 4);
  const dd = +id.slice(4, 6);
  const thisYear = today.getFullYear();
  const century = yy <= thisYear % 100 ? Math.floor(thisYear / 100) * 100 : (Math.floor(thisYear / 100) - 1) * 100;
  const dob = new Date(Date.UTC(century + yy, mm - 1, dd));
  const real = dob.getUTCFullYear() === century + yy && dob.getUTCMonth() === mm - 1 && dob.getUTCDate() === dd;
  if (!real || dob > today) return 'The first six digits must be a real date of birth (YYMMDD).';
  if (!luhn(id)) return "This ID number isn't valid. Please check the digits.";
  return null;
}

export const phoneValidator: ValidatorFn = (c: AbstractControl): ValidationErrors | null =>
  !c.value || saMobile(c.value) ? null : { phone: 'Enter a South African mobile number, e.g. 082 123 4567.' };

/** DX-11: digits, optional leading minus, one decimal separator (comma or full stop); spaces ignored. */
export function parseNumericAnswer(raw: string): number | null {
  const s = raw.replace(/\s/g, '').replace(',', '.').replace('−', '-');
  if (!s || s.length > 30 || !/^-?(\d+(\.\d*)?|\.\d+)$/.test(s)) return null;
  return Number(s);
}
