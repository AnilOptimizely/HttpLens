export interface HttpTrafficRecord {
  id: string;
  timestamp: string;
  duration: string;
  httpClientName: string;
  requestMethod: string;
  requestUri: string;
  requestHeaders: Record<string, string[]>;
  requestBody: string | null;
  requestContentType: string | null;
  requestBodySizeBytes: number | null;
  responseStatusCode: number | null;
  responseHeaders: Record<string, string[]>;
  responseBody: string | null;
  responseContentType: string | null;
  responseBodySizeBytes: number | null;
  isSuccess: boolean;
  exception: string | null;
  traceId: string | null;
  parentSpanId: string | null;
  inboundRequestPath: string | null;
  attemptNumber: number;
  retryGroupId: string | null;
}

export interface TrafficListResponse {
  total: number;
  records: HttpTrafficRecord[];
}

export type StatusClass = 'success' | 'redirect' | 'client-error' | 'server-error' | 'error';
export type DetailTab = 'request' | 'response' | 'headers' | 'timing' | 'correlation' | 'export';

export interface FilterState {
  method: string;
  status: string;
  host: string;
  search: string;
}

export type ConnectionStatus = 'connected' | 'live' | 'reconnecting' | 'disconnected';
