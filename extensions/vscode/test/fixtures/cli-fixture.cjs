const { spawn } = require('node:child_process');

const mode = process.argv[2];

switch (mode) {
  case 'args':
    process.stdout.write(JSON.stringify(process.argv.slice(3)));
    break;
  case 'exit':
    process.stderr.write('controlled failure');
    process.exitCode = Number(process.argv[3]);
    break;
  case 'wait':
    setInterval(() => {}, 1000);
    break;
  case 'ignore-term':
    process.on('SIGTERM', () => {});
    setInterval(() => {}, 1000);
    break;
  case 'process-tree':
    spawn(process.execPath, ['-e', "process.on('SIGTERM', () => {}); setInterval(() => {}, 1000)"], {
      stdio: ['ignore', 'inherit', 'inherit'],
    });
    setInterval(() => {}, 1000);
    break;
  case 'output':
    process.stdout.write('x'.repeat(Number(process.argv[3])));
    break;
  case 'error-output':
    process.stderr.write('x'.repeat(Number(process.argv[3])));
    break;
  default:
    process.stderr.write('unknown fixture mode');
    process.exitCode = 2;
}
