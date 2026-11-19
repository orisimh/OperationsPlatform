import { Component, OnInit } from '@angular/core';
import { DashboardData } from '../models/operation.models';
import { OperationsApiService } from '../services/operations-api.service';

@Component({ selector: 'app-dashboard', templateUrl: './dashboard.component.html', styleUrls: ['./dashboard.component.scss'] })
export class DashboardComponent implements OnInit {
  data: DashboardData | null = null;
  loading = true;
  error = '';
  constructor(private api: OperationsApiService) {}
  ngOnInit(): void {
    this.api.getDashboard().subscribe(value => { this.data = value; this.loading = false; }, () => { this.error = 'Could not load dashboard. Check that the API is running.'; this.loading = false; });
  }
  displayResult(value: string | null): string {
    if (!value) return '—';
    try { const parsed = JSON.parse(value); return typeof parsed === 'string' ? parsed : JSON.stringify(parsed); } catch { return value; }
  }
}
