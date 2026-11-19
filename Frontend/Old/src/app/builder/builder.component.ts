import { CdkDragDrop, moveItemInArray } from '@angular/cdk/drag-drop';
import { Component, OnInit } from '@angular/core';
import { FormBuilder, FormGroup, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatSnackBar } from '@angular/material/snack-bar';
import { OperationsApiService } from '../services/operations-api.service';
import { OperationDefinition, UpsertOperationRequest } from '../models/operation.models';

interface BuilderNode {
  id: string;
  handlerKey: string;
  inputA: string;
  inputB: string;
  configurationJson: string;
}

@Component({ selector: 'app-builder', templateUrl: './builder.component.html', styleUrls: ['./builder.component.scss'] })
export class BuilderComponent implements OnInit {
  form: FormGroup;
  nodes: BuilderNode[] = [];
  loading = false;
  operationId: number | null = null;
  isEdit = false;
  codeError = '';
  nextId = 1;
  outputTypes = ['String', 'Integer', 'Decimal', 'Boolean', 'Json'];
  handlers = [
    { key: 'string.lowercase', label: 'Lowercase' }, { key: 'string.uppercase', label: 'Uppercase' },
    { key: 'string.trim', label: 'Trim' }, { key: 'string.reverse', label: 'Reverse' },
    { key: 'string.concatenate', label: 'Concatenate' }, { key: 'string.contains', label: 'Contains' },
    { key: 'string.equals', label: 'Equals' }, { key: 'string.startsWith', label: 'Starts With' },
    { key: 'string.endsWith', label: 'Ends With' }, { key: 'string.replace', label: 'Replace' },
    { key: 'integer.add', label: 'Add' }, { key: 'integer.subtract', label: 'Subtract' },
    { key: 'integer.multiply', label: 'Multiply' }, { key: 'integer.divide', label: 'Divide' },
    { key: 'integer.modulo', label: 'Modulo' }
  ];

  constructor(private fb: FormBuilder, private api: OperationsApiService, private route: ActivatedRoute, private router: Router, private snack: MatSnackBar) {
    this.form = this.fb.group({ code: ['', [Validators.required, Validators.pattern(/^[a-zA-Z0-9._-]+$/)]], name: ['', Validators.required], description: [''], category: ['Composite'], icon: ['account_tree'], operationType: ['String'], outputType: ['Boolean'] });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) { this.operationId = Number(id); this.isEdit = true; this.load(this.operationId); }
    else this.addNode('string.lowercase', '$fieldA', '$fieldB');
  }

  load(id: number): void {
    this.loading = true;
    this.api.getOperation(id).subscribe(op => {
      this.form.patchValue(op);
      const config = JSON.parse(op.configurationJson || '{}');
      this.nodes = (config.nodes || []).map((n: any) => ({ id: n.id, handlerKey: n.handlerKey, inputA: n.inputA, inputB: n.inputB, configurationJson: JSON.stringify(n.configuration || {}, null, 2) }));
      this.nextId = this.nodes.reduce((max, node) => {
        const match = /^step(\d+)$/.exec(node.id);
        return Math.max(max, match ? Number(match[1]) : 0);
      }, 0) + 1;
      this.loading = false;
    }, () => { this.loading = false; this.snack.open('Could not load Builder operation.', 'Close', { duration: 3500 }); });
  }

  addNode(handlerKey = 'string.lowercase', inputA = '$fieldA', inputB = '$fieldB'): void {
    let id = `step${this.nextId++}`;
    while (this.nodes.some(node => node.id === id)) id = `step${this.nextId++}`;
    this.nodes.push({ id, handlerKey, inputA, inputB, configurationJson: '{}' });
  }
  removeNode(index: number): void { this.nodes.splice(index, 1); }
  drop(event: CdkDragDrop<BuilderNode[]>): void { moveItemInArray(this.nodes, event.previousIndex, event.currentIndex); }
  handlerLabel(key: string): string { return this.handlers.find(x => x.key === key)?.label || key; }
  outputReferences(index: number): string[] { return ['$fieldA', '$fieldB', ...this.nodes.slice(0, index).map(n => `${n.id}.output`)]; }

  save(): void {
    this.codeError = '';
    if (this.form.invalid || this.nodes.length === 0) { this.form.markAllAsTouched(); return; }
    let nodes: any[];
    try {
      nodes = this.nodes.map(node => ({ id: node.id, handlerKey: node.handlerKey, inputA: node.inputA, inputB: node.inputB, configuration: JSON.parse(node.configurationJson || '{}') }));
    } catch { this.codeError = 'One of the node configuration fields contains invalid JSON.'; return; }
    const request: UpsertOperationRequest = {
      code: this.form.value.code, name: this.form.value.name, description: this.form.value.description,
      operationType: this.form.value.operationType, implementationType: 'Builder', executionMode: 'Server',
      inputAConfigJson: JSON.stringify({ type: this.form.value.operationType === 'Integer' ? 'Integer' : 'String', label: 'Field A', required: true }),
      inputBConfigJson: JSON.stringify({ type: this.form.value.operationType === 'Integer' ? 'Integer' : 'String', label: 'Field B', required: true }),
      outputType: this.form.value.outputType, category: this.form.value.category, icon: this.form.value.icon,
      isActive: true, sortOrder: 500,
      configurationJson: JSON.stringify({ nodes, return: `${this.nodes[this.nodes.length - 1].id}.output` })
    };
    this.loading = true;
    const call = this.isEdit && this.operationId ? this.api.updateOperation(this.operationId, request) : this.api.createOperation(request);
    call.subscribe(() => { this.loading = false; this.snack.open('Builder operation saved.', 'Close', { duration: 3000 }); this.router.navigate(['/operations']); }, error => { this.loading = false; this.codeError = error?.error?.detail || 'Could not save the Builder operation.'; });
  }
}
