import * as React from 'react';

const TOAST_LIMIT = 5;
const TOAST_REMOVE_DELAY = 2500;

type ToastActionElement = React.ReactElement;

export interface ToastInput {
  id?: string;
  title?: React.ReactNode;
  description?: React.ReactNode;
  variant?: 'default' | 'destructive' | 'success';
  action?: ToastActionElement;
  duration?: number;
}

const actionTypes = { ADD_TOAST: 'ADD_TOAST', UPDATE_TOAST: 'UPDATE_TOAST', DISMISS_TOAST: 'DISMISS_TOAST', REMOVE_TOAST: 'REMOVE_TOAST' } as const;

let count = 0;
function genId() { count = (count + 1) % Number.MAX_SAFE_INTEGER; return count.toString(); }

type Action =
  | { type: typeof actionTypes.ADD_TOAST; toast: ToastInput }
  | { type: typeof actionTypes.UPDATE_TOAST; toast: Partial<ToastInput>; id: string }
  | { type: typeof actionTypes.DISMISS_TOAST; id?: string }
  | { type: typeof actionTypes.REMOVE_TOAST; id?: string };

interface State { toasts: ToastInput[]; }
function reducer(state: State, action: Action): State {
  switch (action.type) {
    case actionTypes.ADD_TOAST: return { ...state, toasts: [action.toast, ...state.toasts].slice(0, TOAST_LIMIT) };
    case actionTypes.DISMISS_TOAST: return { ...state, toasts: state.toasts.map((t) => action.id === t.id ? { ...t, id: t.id } : t) };
    case actionTypes.REMOVE_TOAST: return { ...state, toasts: action.id ? state.toasts.filter((t) => t.id !== action.id) : state.toasts };
    default: return state;
  }
}

const listeners: Array<(state: State) => void> = [];
let memoryState: State = { toasts: [] };

function dispatch(action: Action) {
  memoryState = reducer(memoryState, action);
  listeners.forEach((l) => l(memoryState));
}

function toast({ id, duration = TOAST_REMOVE_DELAY, ...props }: ToastInput = {}) {
  const _id = id ?? genId();
  // duration is kept on the entry too: it is forwarded to Radix's Toast so its own
  // close timer matches ours (uncontrolled Radix would otherwise self-hide at the
  // provider default and cut long toasts short while they are still in the list)
  dispatch({ type: actionTypes.ADD_TOAST, toast: { ...props, id: _id, duration } });
  setTimeout(() => dispatch({ type: actionTypes.REMOVE_TOAST, id: _id }), duration);
  return { id: _id, dismiss: () => dispatch({ type: actionTypes.REMOVE_TOAST, id: _id }) };
}

function useToast() {
  const [state, setState] = React.useState<State>(memoryState);
  React.useEffect(() => {
    listeners.push(setState);
    return () => { const i = listeners.indexOf(setState); if (i > -1) listeners.splice(i, 1); };
  }, [state]);
  return { ...state, toast, dismiss: (id?: string) => dispatch({ type: actionTypes.REMOVE_TOAST, id }) };
}

export { useToast, toast };
