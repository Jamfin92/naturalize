import { useCallback, useEffect, useState } from 'react'

interface AsyncState<T> {
  data: T | null
  error: string | null
  loading: boolean
  reload: () => void
}

/**
 * Minimal fetch-on-mount hook. Deliberately not TanStack Query — there is no
 * cache to invalidate, no optimistic update, and no background refetch in this
 * app, so a query library would be ceremony with nothing to hold.
 */
export function useAsync<T>(fn: () => Promise<T>, deps: unknown[]): AsyncState<T> {
  const [data, setData] = useState<T | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [loading, setLoading] = useState(true)
  const [nonce, setNonce] = useState(0)

  // eslint-disable-next-line react-hooks/exhaustive-deps
  const run = useCallback(fn, deps)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)

    run()
      .then((result) => {
        if (!cancelled) setData(result)
      })
      .catch((e: unknown) => {
        if (!cancelled) setError(e instanceof Error ? e.message : 'Request failed')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    return () => {
      cancelled = true
    }
  }, [run, nonce])

  return { data, error, loading, reload: () => setNonce((n) => n + 1) }
}
