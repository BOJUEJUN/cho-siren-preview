const test = require('node:test');
const assert = require('node:assert/strict');
const {
  MEMBERS,
  DEFAULT_STATE,
  PERFORMANCE_STAMINA_COST,
  STAMINA_REGEN_MS,
  dayKey,
  normalizeState,
  drawMember,
  recruit,
  memberPower,
  signCandidate,
  trainMember,
  resolvePerformance,
  claimDailySignIn,
  dailyTaskStatus,
  claimDailyTask,
  createBackup,
  restoreBackup
} = require('./game-core.js');

test('member ids are unique and core roles are represented', () => {
  assert.equal(new Set(MEMBERS.map(member => member.id)).size, MEMBERS.length);
  assert.equal(new Set(MEMBERS.map(member => member.image)).size, MEMBERS.length);
  assert.deepEqual(new Set(MEMBERS.map(member => member.role)), new Set(['主唱', '舞者', '支援']));
  assert.ok(MEMBERS.every(member => member.personality && member.value && member.vocal > 0 && member.dance > 0));
});

test('audition signing chooses the selected candidate instead of a random member', () => {
  const outcome = signCandidate(DEFAULT_STATE, 'chuxue', 150, () => 2468);
  assert.equal(outcome.error, null);
  assert.equal(outcome.member.id, 'chuxue');
  assert.equal(outcome.isNew, true);
  assert.equal(outcome.state.gems, DEFAULT_STATE.gems - 150);
  assert.equal(outcome.state.owned.chuxue, 1);
  assert.deepEqual(outcome.state.history[0], { at: 2468, count: 1, members: ['chuxue'] });
});

test('member training spends coins and permanently raises power', () => {
  const before = memberPower(DEFAULT_STATE, 'xingli');
  const outcome = trainMember(DEFAULT_STATE, 'xingli', 600);
  assert.equal(outcome.error, null);
  assert.equal(outcome.state.coins, DEFAULT_STATE.coins - 600);
  assert.equal(outcome.state.training.xingli, 1);
  assert.equal(memberPower(outcome.state, 'xingli'), before + 420);
});

test('invalid saves recover to a playable starter state', () => {
  const recovered = normalizeState({ gems: -20, owned: { unknown: 99 }, team: ['unknown'], leaderId: 'unknown' });
  assert.equal(recovered.gems, 0);
  assert.deepEqual(recovered.owned, DEFAULT_STATE.owned);
  assert.equal(recovered.team.length, 4);
  assert.ok(recovered.team.includes(recovered.leaderId));
});

test('saved teams discard locked or unknown members and keep a valid leader', () => {
  const state = normalizeState({
    gems: 500,
    owned: { xingli: 1, yeying: 2 },
    team: ['unknown', 'yeying', 'xingli'],
    leaderId: 'yeying'
  });
  assert.deepEqual(state.team, ['yeying', 'xingli']);
  assert.equal(state.leaderId, 'yeying');
});

test('single recruit deducts currency and records the obtained member', () => {
  const outcome = recruit(DEFAULT_STATE, 1, 150, () => 0.99, () => 1234);
  assert.equal(outcome.error, null);
  assert.equal(outcome.state.gems, DEFAULT_STATE.gems - 150);
  assert.equal(outcome.results.length, 1);
  assert.equal(outcome.state.history[0].at, 1234);
  assert.equal(outcome.state.history[0].members[0], outcome.results[0].member.id);
});

test('ten recruit guarantees at least SR when random rolls are all R', () => {
  const outcome = recruit(DEFAULT_STATE, 10, 1500, () => 0.99, () => 5678);
  assert.equal(outcome.results.length, 10);
  assert.ok(outcome.results.some(result => result.member.rarity === 'SR' || result.member.rarity === 'SSR'));
});

test('insufficient currency never mutates the input or creates history', () => {
  const poorState = normalizeState({ ...DEFAULT_STATE, gems: 10 });
  const snapshot = JSON.stringify(poorState);
  const outcome = recruit(poorState, 1, 150, () => 0.5, () => 1);
  assert.equal(outcome.error, 'INSUFFICIENT_GEMS');
  assert.equal(outcome.missing, 140);
  assert.equal(JSON.stringify(poorState), snapshot);
  assert.equal(outcome.results.length, 0);
});

test('rarity thresholds remain deterministic under injected random values', () => {
  assert.equal(drawMember(() => 0.01).rarity, 'SSR');
  assert.equal(drawMember(() => 0.10).rarity, 'SR');
  assert.equal(drawMember(() => 0.90).rarity, 'R');
});

test('stamina recovers once every five minutes without exceeding the cap', () => {
  const savedAt = 1000;
  const state = normalizeState({ ...DEFAULT_STATE, stamina: 98, staminaUpdatedAt: savedAt }, savedAt + STAMINA_REGEN_MS * 3 + 100);
  assert.equal(state.stamina, 101);
  assert.equal(state.staminaUpdatedAt, savedAt + STAMINA_REGEN_MS * 3);
});

test('performance creates the recruit reward loop and stores the best score', () => {
  const before = normalizeState({ ...DEFAULT_STATE, stamina: 70, staminaUpdatedAt: 5000 }, 5000);
  const outcome = resolvePerformance(before, { quality: 9.5, totalNotes: 10, teamPower: 66430 }, () => 5000);
  assert.equal(outcome.error, null);
  assert.equal(outcome.rank, 'S');
  assert.equal(outcome.state.stamina, 70 - PERFORMANCE_STAMINA_COST);
  assert.equal(outcome.state.gems, before.gems + outcome.rewards.gems);
  assert.equal(outcome.state.coins, before.coins + outcome.rewards.coins);
  assert.equal(outcome.state.performanceCount, 1);
  assert.equal(outcome.state.bestScore, outcome.score);
  assert.equal(outcome.state.performanceHistory[0].rank, 'S');
});

test('performance refuses to start without stamina and preserves rewards', () => {
  const poorState = normalizeState({ ...DEFAULT_STATE, stamina: 4, staminaUpdatedAt: 9000 }, 9000);
  const outcome = resolvePerformance(poorState, { quality: 10, totalNotes: 10, teamPower: 66430 }, () => 9000);
  assert.equal(outcome.error, 'INSUFFICIENT_STAMINA');
  assert.equal(outcome.missing, PERFORMANCE_STAMINA_COST - 4);
  assert.equal(outcome.state.gems, poorState.gems);
  assert.equal(outcome.state.performanceCount, 0);
});

test('daily sign-in grants one reward and rejects a second claim', () => {
  const offset = -480;
  const now = Date.UTC(2026, 8, 1, 2);
  const first = claimDailySignIn(DEFAULT_STATE, () => now, offset);
  assert.equal(first.error, null);
  assert.equal(first.reward, 100);
  assert.equal(first.state.gems, DEFAULT_STATE.gems + 100);
  assert.equal(first.state.signInStreak, 1);
  assert.equal(first.state.lastSignInDay, dayKey(now, offset));

  const duplicate = claimDailySignIn(first.state, () => now + 1000, offset);
  assert.equal(duplicate.error, 'ALREADY_CLAIMED');
  assert.equal(duplicate.state.gems, first.state.gems);
});

test('sign-in streak grows on consecutive days and resets after a gap', () => {
  const offset = -480;
  const dayOne = Date.UTC(2026, 8, 1, 2);
  const first = claimDailySignIn(DEFAULT_STATE, () => dayOne, offset);
  const second = claimDailySignIn(first.state, () => dayOne + 24 * 60 * 60 * 1000, offset);
  assert.equal(second.state.signInStreak, 2);
  assert.equal(second.reward, 110);
  const afterGap = claimDailySignIn(second.state, () => dayOne + 3 * 24 * 60 * 60 * 1000, offset);
  assert.equal(afterGap.state.signInStreak, 1);
  assert.equal(afterGap.reward, 100);
});

test('daily task requires three performances, pays once, and resets next day', () => {
  const offset = -480;
  const now = Date.UTC(2026, 8, 1, 2);
  const today = dayKey(now, offset);
  const base = normalizeState({
    ...DEFAULT_STATE,
    performanceCount: 4,
    dailyTaskDay: today,
    dailyTaskBasePerformanceCount: 4
  }, now, offset);
  const almost = normalizeState({ ...base, performanceCount: 6 }, now, offset);
  const earlyClaim = claimDailyTask(almost, () => now, offset);
  assert.equal(earlyClaim.error, 'NOT_READY');
  assert.equal(earlyClaim.missing, 1);

  const readyState = normalizeState({ ...base, performanceCount: 7 }, now, offset);
  assert.equal(dailyTaskStatus(readyState, () => now, offset).ready, true);
  const claim = claimDailyTask(readyState, () => now, offset);
  assert.equal(claim.error, null);
  assert.equal(claim.state.gems, readyState.gems + claim.reward.gems);
  assert.equal(claim.state.coins, readyState.coins + claim.reward.coins);
  assert.equal(claimDailyTask(claim.state, () => now, offset).error, 'ALREADY_CLAIMED');

  const tomorrow = now + 24 * 60 * 60 * 1000;
  const reset = dailyTaskStatus(claim.state, () => tomorrow, offset);
  assert.equal(reset.progress, 0);
  assert.equal(reset.claimed, false);
});

test('versioned backup round-trips state and accessibility preferences', () => {
  const now = Date.UTC(2026, 8, 1, 2);
  const source = normalizeState({
    ...DEFAULT_STATE,
    gems: 4321,
    coins: 9876,
    stamina: 77,
    staminaUpdatedAt: now,
    performanceCount: 8,
    bestScore: 76543
  }, now);
  const backup = createBackup(source, { motionEnabled: false, reducedEffects: true }, () => now);
  const restored = restoreBackup(JSON.stringify(backup), () => now);
  assert.equal(restored.error, null);
  assert.equal(restored.version, 1);
  assert.equal(restored.state.gems, 4321);
  assert.equal(restored.state.coins, 9876);
  assert.equal(restored.state.stamina, 77);
  assert.equal(restored.state.performanceCount, 8);
  assert.deepEqual(restored.preferences, { motionEnabled: false, reducedEffects: true });
});

test('backup restore rejects malformed and future save formats', () => {
  assert.equal(restoreBackup('{bad json').error, 'INVALID_BACKUP');
  assert.equal(restoreBackup({ format: 'cho-siren-save', version: 1, state: { gems: 10 } }).error, 'INVALID_BACKUP');
  assert.equal(restoreBackup({ format: 'cho-siren-save', version: 99, state: DEFAULT_STATE }).error, 'UNSUPPORTED_VERSION');
});

test('legacy direct-state backup remains importable', () => {
  const restored = restoreBackup({ ...DEFAULT_STATE, gems: 2468 }, () => 5000);
  assert.equal(restored.error, null);
  assert.equal(restored.version, 0);
  assert.equal(restored.state.gems, 2468);
  assert.deepEqual(restored.preferences, { motionEnabled: true, reducedEffects: false });
});
