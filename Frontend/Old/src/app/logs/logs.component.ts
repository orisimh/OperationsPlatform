import { Component, OnInit } from '@angular/core';
import { ApplicationLog, PagedResult } from '../models/operation.models';
import { OperationsApiService } from '../services/operations-api.service';

@Component({ selector: 'app-logs', templateUrl: './logs.component.html', styleUrls: ['./logs.component.scss'] })
export class LogsComponent implements OnInit {
  result: PagedResult<ApplicationLog> | null = null;
  loading = true;
  page = 1;
  pageSize = 30;
  expandedId: number | null = null;
  constructor(private api: OperationsApiService) {}
  ngOnInit(): void { this.load(); }
  load(): void { this.loading = true; this.api.getLogs(this.page, this.pageSize).subscribe(result => { this.result = result; this.loading = false; }, () => this.loading = false); }
  next(delta: number): void { this.page = Math.max(1, this.page + delta); this.load(); }
  toggle(id: number): void { this.expandedId = this.expandedId === id ? null : id; }
}
