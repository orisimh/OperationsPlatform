export interface HandlerDescriptor {
  key: string;
  label: string;
}

export interface OperationDefinition {
  id: number;
  code: string;
  name: string;
  description: string;
  operationType: string;
  implementationType: string;
  executionMode: string;
  inputAConfigJson: string;
  inputBConfigJson: string;
  outputType: string;
  configurationJson: string;
  category: string;
  icon: string;
  isActive: boolean;
  sortOrder: number;
  version: number;
  createdAtUtc: string;
  updatedAtUtc: string;
}

export interface UpsertOperationRequest {
  code: string;
  name: string;
  description: string;
  operationType: string;
  implementationType: string;
  executionMode: string;
  inputAConfigJson: string;
  inputBConfigJson: string;
  outputType: string;
  configurationJson: string;
  category: string;
  icon: string;
  isActive: boolean;
  sortOrder: number;
}

export interface HistoryItem {
  id: number;
  operationId: number | null;
  operationName: string;
  operationCode: string;
  operationType: string;
  operationVersion: number;
  inputA: string;
  inputB: string;
  resultJson: string | null;
  status: string;
  errorMessage: string | null;
  executionDurationMs: number;
  executedAtUtc: string;
}

export interface OperationExecutionResponse {
  success: boolean;
  result: any;
  errorMessage: string | null;
  executionDurationMs: number;
  executionId: number;
  operationName: string;
  lastThreeExecutions: HistoryItem[];
  monthlyExecutionCount: number;
  executionTrace: any;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ApplicationLog {
  id: number;
  level: string;
  source: string;
  message: string;
  correlationId: string;
  requestMethod: string | null;
  requestPath: string | null;
  statusCode: number | null;
  requestHeadersJson: string | null;
  responseHeadersJson: string | null;
  requestBody: string | null;
  responseBody: string | null;
  exception: string | null;
  createdAtUtc: string;
}

export interface DashboardData {
  activeOperations: number;
  executionsThisMonth: number;
  failedExecutionsThisMonth: number;
  successRate: number;
  recentExecutions: HistoryItem[];
  mostUsedOperations: { name: string; count: number }[];
}

export interface InputConfig {
  type?: string;
  label?: string;
  required?: boolean;
  placeholder?: string;
  min?: number;
  max?: number;
}
