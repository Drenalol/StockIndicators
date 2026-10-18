import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/services/api';
import { qk } from '@/lib/query';

/**
 * The settings-page guide's dismissal flag, one shared query: the guide card hides
 * itself through it, the page header offers the way back through the same cache entry.
 * The flag lives in the database, so the choice holds on every device.
 */
export function useOnboardingState() {
  const queryClient = useQueryClient();

  const state = useQuery({
    queryKey: qk.onboarding,
    queryFn: () => api.getOnboarding(),
  });

  const setHidden = useMutation({
    mutationFn: (hidden: boolean) => api.setOnboarding({ hidden }),
    onSuccess: (_result, hidden) => queryClient.setQueryData(qk.onboarding, { hidden }),
  });

  return {
    hidden: !!state.data?.hidden,
    isSettled: !!state.data,
    setHidden: (hidden: boolean) => void setHidden.mutate(hidden),
  };
}
