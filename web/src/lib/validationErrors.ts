import type { Translations } from '@/i18n';
import type { ApiValidationError } from '@/services/api';
import { getIndicatorShortName, getParameterLabel } from '@/i18n/indicatorTranslations';

type ArgValue = string | number | boolean | null;

function argStr(args: Record<string, ArgValue>, key: string): string | undefined {
  const value = args[key];
  return value == null ? undefined : String(value);
}

// Substitutes {placeholder} tokens without a regex so literal braces in a value stay safe.
function fill(template: string, map: Record<string, string | undefined>): string {
  return Object.entries(map).reduce((acc, [key, value]) => acc.split(`{${key}}`).join(value ?? ''), template);
}

/**
 * Maps one machine-readable validation code (sent by the backend in "errorDetails") to a
 * localized sentence. Parameter labels and indicator names are localized through the shared
 * indicator i18n helpers; an unknown code falls back to the English message the backend kept.
 */
function localizeCode(err: ApiValidationError, t: Translations): string {
  const { args, code } = err;
  const v = t.validation;
  const paramLabel = argStr(args, 'parameter') ? getParameterLabel(argStr(args, 'parameter') as string, t) : '';

  switch (code) {
    case 'template.paramMin':
      return fill(v.paramAtLeast, { param: paramLabel, min: argStr(args, 'min') });
    case 'template.paramPositive':
      return fill(v.paramPositive, { param: paramLabel });
    case 'template.paramNonNegative':
      return fill(v.paramNonNegative, { param: paramLabel });
    case 'template.periodOutOfRange':
      return fill(v.periodOutOfRange, { max: argStr(args, 'max') });
    case 'template.format':
      return v.templateFormat;
    case 'template.unrecognized':
      return v.templateUnrecognized;
    case 'template.unknown':
      return v.templateUnknown;
    case 'rule.nameRequired':
      return v.ruleNameRequired;
    case 'rule.nameTooLong':
      return fill(v.ruleNameTooLong, { max: argStr(args, 'max') });
    case 'rule.atLeastOneCondition':
      return v.atLeastOneCondition;
    case 'rule.conditionsLimit':
      return fill(v.conditionsLimit, { max: argStr(args, 'max'), found: argStr(args, 'found') });
    case 'rule.nestingTooDeep':
      return fill(v.nestingTooDeep, { max: argStr(args, 'max') });
    case 'rule.groupRequired':
      return v.groupRequired;
    case 'rule.indicatorRequired':
      return v.indicatorRequired;
    case 'rule.instrumentRequired':
      return v.instrumentRequired;
    case 'instrument.notFound':
      return fill(v.instrumentNotFound, { ticker: argStr(args, 'ticker') });
    case 'rule.tooManyActions':
      return fill(v.tooManyActions, { max: argStr(args, 'max') });
    case 'rule.duplicateAction':
      return fill(v.duplicateAction, { type: argStr(args, 'type') });
    default:
      return err.message ?? code;
  }
}

/**
 * Turns the structured validation errors from the API into localized messages. A condition
 * parameter error carries the indicator and ticker in its args, so it is wrapped in the
 * "Condition SMA (AAPL): ..." template; everything else renders on its own.
 */
export function localizeValidationErrors(errors: ApiValidationError[], t: Translations): string[] {
  return errors.map((err) => {
    const inner = localizeCode(err, t);
    const indicator = argStr(err.args, 'indicator');

    if (indicator) {
      return fill(t.validation.conditionError, {
        indicator: getIndicatorShortName(indicator),
        ticker: argStr(err.args, 'ticker') ?? '',
        error: inner,
      });
    }

    return inner;
  });
}
