import assert from 'node:assert/strict';
import { setTimeout as delay } from 'node:timers/promises';
import { describe, it } from 'node:test';
import { ValidationScheduler } from '../../src/validation/scheduler';

describe('ValidationScheduler', () => {
  it('debounces save bursts into one validation', async () => {
    let runs = 0;
    const scheduler = new ValidationScheduler(() => { runs += 1; return Promise.resolve(); }, (error) => { throw error; });
    scheduler.schedule(20);
    scheduler.schedule(20);
    scheduler.schedule(20);
    await delay(60);
    assert.equal(runs, 1);
    scheduler.dispose();
  });

  it('cancels an obsolete active validation before starting the newest request', async () => {
    const states: string[] = [];
    const scheduler = new ValidationScheduler(async (signal) => {
      states.push('start');
      await new Promise<void>((resolve) => signal.addEventListener('abort', () => { states.push('abort'); resolve(); }, { once: true }));
    }, (error) => { throw error; });
    scheduler.schedule(5);
    await delay(15);
    scheduler.schedule(5);
    await delay(15);
    assert.deepEqual(states.slice(0, 3), ['start', 'abort', 'start']);
    scheduler.dispose();
  });

  it('does not run after disposal and suppresses aborted errors', async () => {
    let runs = 0;
    let errors = 0;
    const scheduler = new ValidationScheduler(() => { runs += 1; return Promise.resolve(); }, () => { errors += 1; });
    scheduler.schedule(20);
    scheduler.dispose();
    await delay(40);
    assert.equal(runs, 0);
    assert.equal(errors, 0);
  });
});
