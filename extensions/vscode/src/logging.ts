import * as vscode from 'vscode';

export class OperationalLog implements vscode.Disposable {
  private readonly channel = vscode.window.createOutputChannel('ADR Guard');

  public info(message: string): void {
    this.channel.appendLine(`[${new Date().toISOString()}] ${message}`);
  }

  public show(): void {
    this.channel.show(true);
  }

  public detail(title: string, content: string): void {
    this.channel.appendLine(`[${new Date().toISOString()}] ${title}`);
    this.channel.appendLine(content);
    this.channel.show(true);
  }

  public dispose(): void {
    this.channel.dispose();
  }
}
