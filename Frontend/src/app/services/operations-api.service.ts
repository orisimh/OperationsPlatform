import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../environments/environment';
import {
  ApplicationLog, DashboardData, HistoryItem, OperationDefinition,
  OperationExecutionResponse, PagedResult, UpsertOperationRequest
} from '../models/operation.models';

@Injectable({ providedIn: 'root' })
export class OperationsApiService {
  private readonly baseUrl = environment.apiBaseUrl;
  constructor(private http: HttpClient) {}

  getOperations(includeInactive = false): Observable<OperationDefinition[]> {
    return this.http.get<OperationDefinition[]>(`${this.baseUrl}/operations`, { params: { includeInactive: String(includeInactive) } });
  }
  getOperation(id: number): Observable<OperationDefinition> {
    return this.http.get<OperationDefinition>(`${this.baseUrl}/operations/${id}`);
  }
  createOperation(request: UpsertOperationRequest): Observable<OperationDefinition> {
    return this.http.post<OperationDefinition>(`${this.baseUrl}/operations`, request);
  }
  updateOperation(id: number, request: UpsertOperationRequest): Observable<OperationDefinition> {
    return this.http.put<OperationDefinition>(`${this.baseUrl}/operations/${id}`, request);
  }
  deleteOperation(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/operations/${id}`);
  }
  execute(id: number, fieldA: string, fieldB: string): Observable<OperationExecutionResponse> {
    return this.http.post<OperationExecutionResponse>(`${this.baseUrl}/operations/${id}/execute`, { fieldA, fieldB });
  }
  validateCSharp(sourceCode: string): Observable<{ success: boolean; message: string; diagnostics: string[] }> {
    return this.http.post<any>(`${this.baseUrl}/operations/validate-csharp`, { sourceCode });
  }
  getHistory(page = 1, pageSize = 20, operationId?: number): Observable<PagedResult<HistoryItem>> {
    let params = new HttpParams().set('page', String(page)).set('pageSize', String(pageSize));
    if (operationId != null) params = params.set('operationId', String(operationId));
    return this.http.get<PagedResult<HistoryItem>>(`${this.baseUrl}/history`, { params });
  }
  getLogs(page = 1, pageSize = 30): Observable<PagedResult<ApplicationLog>> {
    return this.http.get<PagedResult<ApplicationLog>>(`${this.baseUrl}/logs`, { params: { page: String(page), pageSize: String(pageSize) } });
  }
  getDashboard(): Observable<DashboardData> {
    return this.http.get<DashboardData>(`${this.baseUrl}/dashboard`);
  }
}
