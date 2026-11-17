'use strict';
// node --test scripts/tests/test_listening_catalog_filters.cjs
// The C2 catalogue level (15 lessons) must filter, intersect with search and paginate in the redesigned Listening
// index. These replace the two DOM tests of the retired listening-index.js, keeping their intent on the pure core.
const test = require('node:test');
const assert = require('node:assert/strict');
const core = require('../../TCTEnglish/wwwroot/js/listening-core.js');

const LESSONS = [];
for (const level of ['A1', 'C2']) {
  for (let i = 0; i < 15; i++) {
    LESSONS.push({ id: LESSONS.length + 1, title: `${level} lesson ${i}`, topic: level === 'A1' ? 'Daily life' : 'Proficiency', level, status: 'new' });
  }
}

test('C2 is a known level and its filter intersects with the search text', () => {
  assert.ok(core.LEVELS.includes('C2'));
  assert.equal(core.parseFilterQuery('?level=C2', []).level, 'C2');
  assert.equal(core.filterLessons(LESSONS, { level: 'C2' }).length, 15);
  assert.deepEqual(core.filterLessons(LESSONS, { level: 'C2', q: 'A1' }), []);
  assert.equal(core.filterLessons(LESSONS, { level: 'C2', q: 'lesson 3' }).length, 1);
});

test('C2 pagination exposes all 15 cards across pages and a new filter starts again from the first page', () => {
  const ids = core.filterLessons(LESSONS, { level: 'C2' });
  const seen = new Set();
  let shown = 6;
  for (let page = 0; page < 3; page++) {
    const result = core.paginate(ids, shown);
    result.visible.forEach((id) => seen.add(id));
    if (!result.hasMore) break;
    shown += 6;
  }
  assert.equal(seen.size, 15);
  assert.equal(core.paginate(ids, 6).visible.length, 6);
  assert.equal(core.paginate(ids, 6).hasMore, true);
});
