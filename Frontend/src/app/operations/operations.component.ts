import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { MatSnackBar } from '@angular/material/snack-bar';
import { finalize } from 'rxjs/operators';
import { OperationDefinition } from '../models/operation.models';
import { OperationsApiService } from '../services/operations-api.service';

@Component({ selector: 'app-operations', templateUrl: './operations.component.html', styleUrls: ['./operations.component.scss'], standalone: false })
export class OperationsComponent implements OnInit {
  operations: OperationDefinition[] = [];
  loading = true;
  search = '';
  category = 'All';
  categories: string[] = ['All'];
  constructor(private api: OperationsApiService, private snack: MatSnackBar, private cdr: ChangeDetectorRef) {}
  ngOnInit(): void { this.load(); }
  load(): void {
    this.loading = true;
    this.api.getOperations(false).pipe(finalize(() => {
      this.loading = false;
      this.cdr.markForCheck();
    })).subscribe({
      next: items => {
        this.operations = items;
        this.categories = ['All', ...Array.from(new Set(items.map(x => x.category))).sort()];
        this.cdr.markForCheck();
      },
      error: () => this.snack.open('Unable to load operations. Check the API connection.', 'Close', { duration: 4500 })
    });
  }
  get filtered(): OperationDefinition[] {
    const term = this.search.toLowerCase().trim();
    return this.operations.filter(x => (this.category === 'All' || x.category === this.category) &&
      (!term || `${x.name} ${x.description} ${x.code} ${x.operationType}`.toLowerCase().includes(term)));
  }
  remove(item: OperationDefinition): void {
    if (!confirm(`Disable “${item.name}”? Its execution history will be preserved.`)) return;
    this.api.deleteOperation(item.id).subscribe(() => { this.snack.open('Operation disabled. History was preserved.', 'Close', { duration: 3000 }); this.load(); }, () => this.snack.open('Could not disable operation.', 'Close', { duration: 3500 }));
  }
  typeClass(type: string): string { return type.toLowerCase() === 'integer' ? 'math' : type.toLowerCase() === 'restapi' ? 'api' : 'string'; }
  implementationLabel(item: OperationDefinition): string { return item.implementationType === 'CSharp' ? 'Custom C#' : item.implementationType; }
}
