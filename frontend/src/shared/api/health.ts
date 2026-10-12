export type HealthResponse = { status: string; message: string; checkedAt: string };

// Database readiness intentionally returns 503 with a useful JSON response.
export async function getHealth(path: string): Promise<HealthResponse> {
  const response = await fetch(path, { signal: AbortSignal.timeout(6500) });
  const data: unknown = await response.json();
  if (!data || typeof data !== 'object' || !('status' in data) ||
      !('message' in data) || !('checkedAt' in data) ||
      typeof data.status !== 'string' || typeof data.message !== 'string' ||
      typeof data.checkedAt !== 'string') {
    throw new Error('API trả về dữ liệu không đúng định dạng.');
  }
  if (!response.ok && response.status !== 503) throw new Error(`HTTP ${response.status}`);
  return data as HealthResponse;
}
