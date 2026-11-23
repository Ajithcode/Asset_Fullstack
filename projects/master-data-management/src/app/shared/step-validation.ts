/**
 * Shared by the multi-step (tabbed) add/edit forms. Each form validates everything the way it always did; these
 * helpers decide which of those errors belong to the step the user is on, so "Next" can stop on a step with
 * missing mandatory fields instead of leaving every problem for the final Save.
 */

/** Maps an error key to the step (tab) whose template shows that field. */
export type StepOf = (key: string) => string | undefined;

/** The errors that belong to `step`; a key with no known step counts as the first step. */
export function errorsOnStep<E extends Record<string, string | undefined>>(errors: E, step: string, stepOf: StepOf, firstStep: string): E {
  const here: Record<string, string | undefined> = {};
  for (const [key, message] of Object.entries(errors)) {
    if (message && (stepOf(key) ?? firstStep) === step) here[key] = message;
  }
  return here as E;
}

/** The first step (in tab order) that has an error; used to jump there when Save finds problems. */
export function firstStepWithError<S extends string>(errors: Record<string, string | undefined>, steps: readonly { id: S }[], stepOf: StepOf): S {
  const keys = Object.keys(errors).filter((k) => errors[k]);
  return (steps.find((s) => keys.some((k) => (stepOf(k) ?? steps[0].id) === s.id)) ?? steps[0]).id;
}
