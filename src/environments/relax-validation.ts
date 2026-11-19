/**
 * TEMP (testing only): switch that relaxes client-side form validation.
 *
 * Driven by `environment.relaxValidation` (default false). The dev/demo stack (a360demo) builds with it set to true,
 * see /opt/asset360-dev/stack/frontend.Dockerfile. No validation code is removed: every check still runs, its result
 * is simply ignored while the switch is on. Values keep their types (numbers stay numbers, dates stay dates) because
 * only the checks are bypassed, never the conversions that build the API payload. Backend validation is unchanged.
 */
import {
  CheckboxRequiredValidator,
  EmailValidator,
  MaxLengthValidator,
  MaxValidator,
  MinLengthValidator,
  MinValidator,
  PatternValidator,
  RequiredValidator,
  Validators,
  type ValidatorFn,
} from '@angular/forms';
import { environment } from './environment';

export const RELAX_VALIDATION: boolean = !!(environment as { relaxValidation?: boolean }).relaxValidation;

/** Errors object from a form's validate(): returned unchanged normally, as an empty object when relaxed. */
export function relaxErrors<T extends object>(errors: T): T {
  return RELAX_VALIDATION ? ({} as T) : errors;
}

/** A "this field/form is invalid" condition: unchanged normally, always false when relaxed. */
export function relaxInvalid(invalid: boolean): boolean {
  return RELAX_VALIDATION ? false : invalid;
}

/** A "this field/form is valid" condition: unchanged normally, always true when relaxed. */
export function relaxValid(valid: boolean): boolean {
  return RELAX_VALIDATION ? true : valid;
}

/** A single error message (string | null | undefined): unchanged normally, null when relaxed. */
export function relaxError<T>(message: T): T | null {
  return RELAX_VALIDATION ? null : message;
}

let installed = false;

/**
 * Turns off Angular's template validators (required, minlength, maxlength, pattern, email, min, max) and the
 * reactive Validators.* factories, and stops the browser's native HTML5 validation from blocking a submit.
 * Safe to call more than once; does nothing unless the switch is on.
 */
export function installRelaxedValidation(): void {
  if (!RELAX_VALIDATION || installed) return;
  installed = true;

  const noop = () => null;
  for (const directive of [
    RequiredValidator, CheckboxRequiredValidator, EmailValidator, MinLengthValidator,
    MaxLengthValidator, PatternValidator, MinValidator, MaxValidator,
  ]) {
    (directive.prototype as unknown as { validate: () => null }).validate = noop;
  }

  const v = Validators as unknown as Record<string, unknown>;
  const none: ValidatorFn = () => null;
  v['required'] = none;
  v['requiredTrue'] = none;
  v['email'] = none;
  for (const factory of ['min', 'max', 'minLength', 'maxLength', 'pattern']) v[factory] = () => none;

  if (typeof document !== 'undefined') {
    const skipNative = (event: Event) => {
      const form = (event.target as Element | null)?.closest?.('form');
      if (form) form.noValidate = true;
    };
    document.addEventListener('click', skipNative, true);
    document.addEventListener('keydown', skipNative, true);
  }
  console.info('[asset360] TEMP relaxed form validation is ON (testing build)');
}
