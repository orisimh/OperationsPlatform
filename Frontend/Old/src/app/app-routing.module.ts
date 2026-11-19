import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { DashboardComponent } from './dashboard/dashboard.component';
import { OperationsComponent } from './operations/operations.component';
import { OperationEditorComponent } from './operation-editor/operation-editor.component';
import { ExecuteComponent } from './execute/execute.component';
import { BuilderComponent } from './builder/builder.component';
import { HistoryComponent } from './history/history.component';
import { LogsComponent } from './logs/logs.component';

const routes: Routes = [
  { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
  { path: 'dashboard', component: DashboardComponent },
  { path: 'operations', component: OperationsComponent },
  { path: 'operations/new', component: OperationEditorComponent },
  { path: 'operations/:id/edit', component: OperationEditorComponent },
  { path: 'execute/:id', component: ExecuteComponent },
  { path: 'builder', component: BuilderComponent },
  { path: 'builder/:id', component: BuilderComponent },
  { path: 'history', component: HistoryComponent },
  { path: 'logs', component: LogsComponent },
  { path: '**', redirectTo: 'dashboard' }
];

@NgModule({ imports: [RouterModule.forRoot(routes, { relativeLinkResolution: 'legacy' })], exports: [RouterModule] })
export class AppRoutingModule {}
