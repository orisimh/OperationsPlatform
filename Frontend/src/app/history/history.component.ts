import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { HistoryItem, OperationDefinition, PagedResult } from '../models/operation.models';
import { OperationsApiService } from '../services/operations-api.service';

@Component({ selector: 'app-history', templateUrl: './history.component.html', styleUrls: ['./history.component.scss'], standalone: false })
export class HistoryComponent implements OnInit {
  result: PagedResult<HistoryItem> | null = null;
  operations: OperationDefinition[] = [];
  operationId = '';
  page = 1;
  pageSize = 20;
  loading = true;
  constructor(private api: OperationsApiService, private cdr: ChangeDetectorRef) {}
  get totalPages(): number { return Math.max(1, Math.ceil((this.result?.totalCount || 0) / this.pageSize)); }
  ngOnInit(): void { this.api.getOperations(true).subscribe(items => { this.operations = items; this.cdr.markForCheck(); }); this.load(); }
  load(): void {
    this.loading = true;
    this.api.getHistory(this.page, this.pageSize, this.operationId ? Number(this.operationId) : undefined).subscribe(result => { this.result = result; this.loading = false; this.cdr.markForCheck(); }, () => { this.loading = false; this.cdr.markForCheck(); });
  }
  next(delta: number): void { this.page = Math.max(1, this.page + delta); this.load(); }
  display(value: string | null): string { if (!value) return '—'; try { return JSON.stringify(JSON.parse(value)); } catch { return value; } }
}
