import path from 'node:path';
import { realpathSync } from 'node:fs';

export function modelFixture(repo) {
  const fallback = path.join(repo, '.tmp/rebuild/model-check-b20a9ff9d0e640d4ae644aa608330833/data/model');
  const directory = realpathSync(path.resolve(process.env.PHRASEBACK_MODEL_FIXTURE || fallback));
  const temporary = realpathSync(path.join(repo, '.tmp'));
  if (!directory.startsWith(temporary + path.sep)) throw new Error('Native model checks require isolated model assets under the repository .tmp directory');
  return directory;
}
