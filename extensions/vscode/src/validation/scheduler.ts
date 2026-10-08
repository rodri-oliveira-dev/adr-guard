export class ValidationScheduler {
  private timer: NodeJS.Timeout | undefined;
  private active: AbortController | undefined;
  private generation = 0;
  private disposed = false;

  public constructor(
    private readonly run: (signal: AbortSignal) => Promise<void>,
    private readonly onError: (error: unknown) => void,
  ) {}

  public schedule(delayMilliseconds: number): void {
    if (this.disposed) {
      return;
    }
    this.generation += 1;
    const requestedGeneration = this.generation;
    if (this.timer !== undefined) {
      clearTimeout(this.timer);
    }
    this.active?.abort();
    this.timer = setTimeout(() => {
      this.timer = undefined;
      void this.start(requestedGeneration);
    }, delayMilliseconds);
  }

  public cancel(): void {
    this.generation += 1;
    if (this.timer !== undefined) {
      clearTimeout(this.timer);
      this.timer = undefined;
    }
    this.active?.abort();
    this.active = undefined;
  }

  public dispose(): void {
    this.disposed = true;
    this.cancel();
  }

  private async start(requestedGeneration: number): Promise<void> {
    if (this.disposed || requestedGeneration !== this.generation) {
      return;
    }
    const controller = new AbortController();
    this.active = controller;
    try {
      await this.run(controller.signal);
    } catch (error: unknown) {
      if (!controller.signal.aborted && requestedGeneration === this.generation) {
        this.onError(error);
      }
    } finally {
      if (this.active === controller) {
        this.active = undefined;
      }
    }
  }
}
