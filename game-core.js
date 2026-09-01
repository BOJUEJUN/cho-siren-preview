const ChoSirenCore = (() => {
  const MEMBERS = [
    { id: 'xingli', name: '星璃', role: '主唱', level: 68, power: 18680, rarity: 'SSR', image: 'assets/member-xingli.webp', personality: '克制的完美主义者', value: '舞台品质', vocal: 94, dance: 72, charm: 88, discipline: 91, risk: 12 },
    { id: 'feiyin', name: '绯音', role: '舞者', level: 64, power: 17240, rarity: 'SSR', image: 'assets/member-feiyin.webp', personality: '冷静果断的行动派', value: '专业效率', vocal: 70, dance: 95, charm: 82, discipline: 86, risk: 18 },
    { id: 'wubai', name: '雾白', role: '支援', level: 59, power: 15890, rarity: 'SSR', image: 'assets/member-wubai.webp', personality: '神秘而敏锐的观察者', value: '团队信任', vocal: 78, dance: 76, charm: 92, discipline: 80, risk: 24 },
    { id: 'yeying', name: '夜莺', role: '主唱', level: 57, power: 14620, rarity: 'SR', image: 'assets/member-yeying.webp', personality: '温柔但不轻易妥协', value: '真实表达', vocal: 89, dance: 66, charm: 90, discipline: 74, risk: 28 },
    { id: 'chengxia', name: '澄夏', role: '舞者', level: 52, power: 13280, rarity: 'SR', image: 'assets/member-chengxia.webp', personality: '热情外向的气氛担当', value: '观众快乐', vocal: 72, dance: 90, charm: 86, discipline: 68, risk: 34 },
    { id: 'xianyue', name: '弦月', role: '支援', level: 49, power: 12440, rarity: 'SR', image: 'assets/member-xianyue.webp', personality: '细腻稳重的治愈系', value: '成员成长', vocal: 83, dance: 69, charm: 81, discipline: 88, risk: 10 },
    { id: 'hupo', name: '琥珀', role: '主唱', level: 46, power: 11690, rarity: 'R', image: 'assets/member-hupo.webp', personality: '自信张扬的野心家', value: '名气突破', vocal: 84, dance: 73, charm: 89, discipline: 59, risk: 46 },
    { id: 'yaoguang', name: '遥光', role: '舞者', level: 43, power: 10850, rarity: 'R', image: 'assets/member-yaoguang.webp', personality: '沉默可靠的实力派', value: '长期训练', vocal: 65, dance: 87, charm: 70, discipline: 94, risk: 8 },
    { id: 'chuxue', name: '初雪', role: '支援', level: 40, power: 9910, rarity: 'R', image: 'assets/member-chuxue.webp', personality: '古灵精怪的冒险主义者', value: '新鲜体验', vocal: 71, dance: 81, charm: 85, discipline: 61, risk: 42 }
  ];

  const INVENTORY_ITEMS = ['◇', '♬', '✦', '♡', '◈', '♛', '✧', '♫', '◉', '⌁', '♢', '✿', '✪', '♧', '⬡', '❖', '♩', '✺', '◎', '♜'].map((symbol, index) => ({
    symbol,
    type: ['音核', '衣装', '收藏品'][index % 3],
    rarity: index % 3 ? 'SR' : 'SSR',
    count: index + 1
  }));

  const DEFAULT_STATE = {
    gems: 10695,
    coins: 17267,
    stamina: 120,
    staminaUpdatedAt: 0,
    owned: { xingli: 1, feiyin: 1, wubai: 1, yeying: 1 },
    training: {},
    team: ['xingli', 'feiyin', 'wubai', 'yeying'],
    leaderId: 'xingli',
    history: [],
    performanceCount: 0,
    bestScore: 0,
    performanceHistory: [],
    lastSignInDay: '',
    signInStreak: 0,
    lastSignInAt: 0,
    dailyTaskDay: '',
    dailyTaskBasePerformanceCount: 0,
    dailyTaskClaimedDay: ''
  };

  const MAX_STAMINA = 120;
  const PERFORMANCE_STAMINA_COST = 10;
  const STAMINA_REGEN_MS = 5 * 60 * 1000;
  const DAILY_TASK_TARGET = 3;
  const DAY_MS = 24 * 60 * 60 * 1000;
  const SAVE_FORMAT_VERSION = 1;

  const clone = value => JSON.parse(JSON.stringify(value));
  const memberById = id => MEMBERS.find(member => member.id === id);
  const isKnownId = id => Boolean(memberById(id));
  const rankedOwnedIds = state => MEMBERS
    .filter(member => (state.owned[member.id] || 0) > 0)
    .sort((a, b) => b.power - a.power)
    .map(member => member.id);

  const dayKey = (timestamp = Date.now(), timezoneOffsetMinutes) => {
    const time = Math.max(0, Math.floor(Number(timestamp) || Date.now()));
    const offset = Number.isFinite(timezoneOffsetMinutes)
      ? timezoneOffsetMinutes
      : new Date(time).getTimezoneOffset();
    return new Date(time - offset * 60 * 1000).toISOString().slice(0, 10);
  };

  const normalizeState = (input, now = Date.now(), timezoneOffsetMinutes) => {
    const source = input && typeof input === 'object' ? input : {};
    const next = clone(DEFAULT_STATE);
    const currentTime = Math.max(0, Math.floor(Number(now) || Date.now()));

    if (Number.isFinite(source.gems)) next.gems = Math.max(0, Math.floor(source.gems));
    if (Number.isFinite(source.coins)) next.coins = Math.max(0, Math.floor(source.coins));

    if (Number.isFinite(source.stamina)) {
      const savedStamina = Math.max(0, Math.min(MAX_STAMINA, Math.floor(source.stamina)));
      const savedAt = Number.isFinite(source.staminaUpdatedAt)
        ? Math.max(0, Math.min(currentTime, Math.floor(source.staminaUpdatedAt)))
        : currentTime;
      const recovered = Math.floor((currentTime - savedAt) / STAMINA_REGEN_MS);
      next.stamina = Math.min(MAX_STAMINA, savedStamina + recovered);
      next.staminaUpdatedAt = next.stamina >= MAX_STAMINA
        ? currentTime
        : savedAt + recovered * STAMINA_REGEN_MS;
    } else {
      next.stamina = MAX_STAMINA;
      next.staminaUpdatedAt = currentTime;
    }

    if (Number.isFinite(source.performanceCount)) next.performanceCount = Math.max(0, Math.floor(source.performanceCount));
    if (Number.isFinite(source.bestScore)) next.bestScore = Math.max(0, Math.floor(source.bestScore));

    const validDay = value => typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : '';
    next.lastSignInDay = validDay(source.lastSignInDay);
    next.signInStreak = next.lastSignInDay && Number.isFinite(source.signInStreak)
      ? Math.max(0, Math.floor(source.signInStreak))
      : 0;
    next.lastSignInAt = next.lastSignInDay && Number.isFinite(source.lastSignInAt)
      ? Math.max(0, Math.floor(source.lastSignInAt))
      : 0;
    const today = dayKey(currentTime, timezoneOffsetMinutes);
    if (validDay(source.dailyTaskDay) === today) {
      next.dailyTaskDay = today;
      next.dailyTaskBasePerformanceCount = Number.isFinite(source.dailyTaskBasePerformanceCount)
        ? Math.max(0, Math.min(next.performanceCount, Math.floor(source.dailyTaskBasePerformanceCount)))
        : next.performanceCount;
    } else {
      next.dailyTaskDay = today;
      next.dailyTaskBasePerformanceCount = next.performanceCount;
    }
    next.dailyTaskClaimedDay = validDay(source.dailyTaskClaimedDay);

    if (source.owned && typeof source.owned === 'object' && !Array.isArray(source.owned)) {
      const sanitizedOwned = Object.fromEntries(Object.entries(source.owned)
        .filter(([id, count]) => isKnownId(id) && Number.isFinite(count) && count > 0)
        .map(([id, count]) => [id, Math.floor(count)]));
      if (Object.keys(sanitizedOwned).length) next.owned = sanitizedOwned;
    }

    if (source.training && typeof source.training === 'object' && !Array.isArray(source.training)) {
      next.training = Object.fromEntries(Object.entries(source.training)
        .filter(([id, level]) => isKnownId(id) && (next.owned[id] || 0) > 0 && Number.isFinite(level) && level > 0)
        .map(([id, level]) => [id, Math.min(20, Math.floor(level))]));
    }

    if (Array.isArray(source.history)) {
      next.history = source.history.filter(entry => entry && Number.isFinite(entry.at) && Array.isArray(entry.members))
        .map(entry => ({
          at: Math.floor(entry.at),
          count: Math.max(1, Math.floor(Number(entry.count) || entry.members.length || 1)),
          members: entry.members.filter(isKnownId)
        }))
        .filter(entry => entry.members.length)
        .slice(0, 20);
    }

    if (Array.isArray(source.performanceHistory)) {
      next.performanceHistory = source.performanceHistory.filter(entry => entry && Number.isFinite(entry.at) && Number.isFinite(entry.score))
        .map(entry => ({
          at: Math.max(0, Math.floor(entry.at)),
          score: Math.max(0, Math.floor(entry.score)),
          rank: ['S', 'A', 'B', 'C'].includes(entry.rank) ? entry.rank : 'C',
          accuracy: Math.max(0, Math.min(1, Number(entry.accuracy) || 0))
        }))
        .slice(0, 20);
    }

    const ownedIds = rankedOwnedIds(next);
    const savedTeam = Array.isArray(source.team) ? source.team.filter(id => ownedIds.includes(id)) : [];
    next.team = [...new Set([...savedTeam, ...ownedIds])].slice(0, 4);
    next.leaderId = next.team.includes(source.leaderId) ? source.leaderId : next.team[0];
    return next;
  };

  const boundedRandom = random => Math.max(0, Math.min(0.999999, Number(random()) || 0));
  const pickFromRarity = (rarity, random) => {
    const pool = MEMBERS.filter(member => member.rarity === rarity);
    return pool[Math.floor(boundedRandom(random) * pool.length)];
  };

  const drawMember = (random = Math.random, minimumRarity) => {
    if (minimumRarity === 'SR') return pickFromRarity(boundedRandom(random) < 0.18 ? 'SSR' : 'SR', random);
    const roll = boundedRandom(random);
    if (roll < 0.05) return pickFromRarity('SSR', random);
    if (roll < 0.30) return pickFromRarity('SR', random);
    return pickFromRarity('R', random);
  };

  const recruit = (inputState, count, cost, random = Math.random, now = Date.now) => {
    if (!Number.isInteger(count) || count < 1 || !Number.isFinite(cost) || cost < 0) throw new RangeError('Invalid recruit parameters');
    const next = normalizeState(inputState);
    if (next.gems < cost) {
      return { state: next, results: [], error: 'INSUFFICIENT_GEMS', missing: cost - next.gems };
    }

    next.gems -= cost;
    const results = [];
    for (let index = 0; index < count; index += 1) {
      const needsGuarantee = count === 10 && index === count - 1 && !results.some(result => result.member.rarity !== 'R');
      const member = drawMember(random, needsGuarantee ? 'SR' : undefined);
      const previousCount = next.owned[member.id] || 0;
      const ownedCount = previousCount + 1;
      next.owned[member.id] = ownedCount;
      results.push({ member, isNew: previousCount === 0, ownedCount });
    }

    next.history.unshift({ at: Math.floor(Number(now()) || Date.now()), count, members: results.map(result => result.member.id) });
    next.history = next.history.slice(0, 20);
    return { state: normalizeState(next), results, error: null, missing: 0 };
  };

  const teamPower = inputState => {
    const state = normalizeState(inputState);
    return state.team.reduce((total, id) => total + memberById(id).power + (state.training[id] || 0) * 420, 0);
  };

  const memberPower = (inputState, memberId) => {
    const state = normalizeState(inputState);
    const member = memberById(memberId);
    return member ? member.power + (state.training[memberId] || 0) * 420 : 0;
  };

  const signCandidate = (inputState, memberId, cost = 150, now = Date.now) => {
    if (!isKnownId(memberId) || !Number.isFinite(cost) || cost < 0) throw new RangeError('Invalid candidate');
    const next = normalizeState(inputState);
    if (next.gems < cost) return { state: next, error: 'INSUFFICIENT_GEMS', missing: cost - next.gems };
    const previousCount = next.owned[memberId] || 0;
    next.gems -= cost;
    next.owned[memberId] = previousCount + 1;
    next.history.unshift({ at: Math.floor(Number(now()) || Date.now()), count: 1, members: [memberId] });
    return {
      state: normalizeState(next),
      member: memberById(memberId),
      isNew: previousCount === 0,
      ownedCount: previousCount + 1,
      error: null,
      missing: 0
    };
  };

  const trainMember = (inputState, memberId, cost = 600) => {
    if (!isKnownId(memberId) || !Number.isFinite(cost) || cost < 0) throw new RangeError('Invalid training');
    const next = normalizeState(inputState);
    if (!(next.owned[memberId] > 0)) return { state: next, error: 'NOT_OWNED' };
    if (next.coins < cost) return { state: next, error: 'INSUFFICIENT_COINS', missing: cost - next.coins };
    const currentLevel = next.training[memberId] || 0;
    if (currentLevel >= 20) return { state: next, error: 'MAX_TRAINING' };
    next.coins -= cost;
    next.training[memberId] = currentLevel + 1;
    return {
      state: normalizeState(next),
      member: memberById(memberId),
      trainingLevel: currentLevel + 1,
      powerGain: 420,
      error: null,
      missing: 0
    };
  };

  const performanceRank = accuracy => {
    if (accuracy >= 0.9) return 'S';
    if (accuracy >= 0.75) return 'A';
    if (accuracy >= 0.55) return 'B';
    return 'C';
  };

  const resolvePerformance = (inputState, result, now = Date.now) => {
    const currentTime = Math.max(0, Math.floor(Number(now()) || Date.now()));
    const next = normalizeState(inputState, currentTime);
    const totalNotes = Math.floor(Number(result?.totalNotes));
    const quality = Number(result?.quality);
    const power = Math.max(0, Math.floor(Number(result?.teamPower) || teamPower(next)));

    if (!Number.isInteger(totalNotes) || totalNotes < 1 || !Number.isFinite(quality) || quality < 0 || quality > totalNotes) {
      throw new RangeError('Invalid performance result');
    }
    if (next.stamina < PERFORMANCE_STAMINA_COST) {
      return { state: next, error: 'INSUFFICIENT_STAMINA', missing: PERFORMANCE_STAMINA_COST - next.stamina };
    }

    const accuracy = quality / totalNotes;
    const rank = performanceRank(accuracy);
    const score = Math.round(accuracy * 80000 + Math.min(power, 100000) * 0.2);
    const rewardsByRank = {
      S: { gems: 45, coins: 520 },
      A: { gems: 30, coins: 380 },
      B: { gems: 20, coins: 280 },
      C: { gems: 10, coins: 180 }
    };
    const rewards = rewardsByRank[rank];

    next.stamina -= PERFORMANCE_STAMINA_COST;
    next.staminaUpdatedAt = currentTime;
    next.gems += rewards.gems;
    next.coins += rewards.coins;
    next.performanceCount += 1;
    next.bestScore = Math.max(next.bestScore, score);
    next.performanceHistory.unshift({ at: currentTime, score, rank, accuracy });
    next.performanceHistory = next.performanceHistory.slice(0, 20);

    return {
      state: normalizeState(next, currentTime),
      error: null,
      score,
      rank,
      accuracy,
      rewards,
      staminaCost: PERFORMANCE_STAMINA_COST
    };
  };

  const signInStatus = (inputState, now = Date.now, timezoneOffsetMinutes) => {
    const currentTime = Math.max(0, Math.floor(Number(now()) || Date.now()));
    const state = normalizeState(inputState, currentTime, timezoneOffsetMinutes);
    const today = dayKey(currentTime, timezoneOffsetMinutes);
    const yesterday = dayKey(currentTime - DAY_MS, timezoneOffsetMinutes);
    const claimed = state.lastSignInDay === today;
    const nextStreak = claimed
      ? state.signInStreak
      : state.lastSignInDay === yesterday ? state.signInStreak + 1 : 1;
    return {
      state,
      today,
      claimed,
      nextStreak,
      reward: 100 + Math.min(6, Math.max(0, nextStreak - 1)) * 10
    };
  };

  const claimDailySignIn = (inputState, now = Date.now, timezoneOffsetMinutes) => {
    const status = signInStatus(inputState, now, timezoneOffsetMinutes);
    if (status.claimed) return { ...status, error: 'ALREADY_CLAIMED' };
    const next = clone(status.state);
    next.gems += status.reward;
    next.lastSignInDay = status.today;
    next.signInStreak = status.nextStreak;
    next.lastSignInAt = Math.max(0, Math.floor(Number(now()) || Date.now()));
    return {
      ...status,
      state: normalizeState(next, next.lastSignInAt, timezoneOffsetMinutes),
      claimed: true,
      error: null
    };
  };

  const dailyTaskStatus = (inputState, now = Date.now, timezoneOffsetMinutes) => {
    const currentTime = Math.max(0, Math.floor(Number(now()) || Date.now()));
    const state = normalizeState(inputState, currentTime, timezoneOffsetMinutes);
    const today = dayKey(currentTime, timezoneOffsetMinutes);
    const progress = Math.min(DAILY_TASK_TARGET, Math.max(0, state.performanceCount - state.dailyTaskBasePerformanceCount));
    return {
      state,
      today,
      target: DAILY_TASK_TARGET,
      progress,
      ready: progress >= DAILY_TASK_TARGET && state.dailyTaskClaimedDay !== today,
      claimed: state.dailyTaskClaimedDay === today,
      reward: { gems: 120, coins: 800 }
    };
  };

  const claimDailyTask = (inputState, now = Date.now, timezoneOffsetMinutes) => {
    const status = dailyTaskStatus(inputState, now, timezoneOffsetMinutes);
    if (status.claimed) return { ...status, error: 'ALREADY_CLAIMED' };
    if (!status.ready) return { ...status, error: 'NOT_READY', missing: status.target - status.progress };
    const next = clone(status.state);
    next.gems += status.reward.gems;
    next.coins += status.reward.coins;
    next.dailyTaskClaimedDay = status.today;
    return {
      ...status,
      state: normalizeState(next, Number(now()) || Date.now(), timezoneOffsetMinutes),
      ready: false,
      claimed: true,
      error: null,
      missing: 0
    };
  };

  const normalizePreferences = input => ({
    motionEnabled: input?.motionEnabled !== false,
    reducedEffects: Boolean(input?.reducedEffects)
  });

  const createBackup = (inputState, inputPreferences = {}, now = Date.now) => {
    const exportedAt = Math.max(0, Math.floor(Number(now()) || Date.now()));
    return {
      format: 'cho-siren-save',
      version: SAVE_FORMAT_VERSION,
      exportedAt,
      state: normalizeState(inputState, exportedAt),
      preferences: normalizePreferences(inputPreferences)
    };
  };

  const restoreBackup = (input, now = Date.now) => {
    let payload = input;
    if (typeof input === 'string') {
      try {
        payload = JSON.parse(input);
      } catch {
        return { error: 'INVALID_BACKUP', state: null, preferences: null };
      }
    }
    if (!payload || typeof payload !== 'object' || Array.isArray(payload)) {
      return { error: 'INVALID_BACKUP', state: null, preferences: null };
    }
    if (Number.isFinite(payload.version) && payload.version > SAVE_FORMAT_VERSION) {
      return { error: 'UNSUPPORTED_VERSION', state: null, preferences: null };
    }

    const candidate = payload.format === 'cho-siren-save' ? payload.state : payload;
    const hasRecognizableState = candidate && typeof candidate === 'object'
      && Number.isFinite(candidate.gems)
      && candidate.owned && typeof candidate.owned === 'object' && !Array.isArray(candidate.owned)
      && Object.entries(candidate.owned).some(([id, count]) => isKnownId(id) && Number.isFinite(count) && count > 0)
      && Array.isArray(candidate.team);
    if (!hasRecognizableState) {
      return { error: 'INVALID_BACKUP', state: null, preferences: null };
    }

    return {
      error: null,
      version: Number.isFinite(payload.version) ? Math.floor(payload.version) : 0,
      state: normalizeState(candidate, Number(now()) || Date.now()),
      preferences: normalizePreferences(payload.preferences)
    };
  };

  return {
    MEMBERS,
    INVENTORY_ITEMS,
    DEFAULT_STATE,
    MAX_STAMINA,
    PERFORMANCE_STAMINA_COST,
    STAMINA_REGEN_MS,
    DAILY_TASK_TARGET,
    SAVE_FORMAT_VERSION,
    dayKey,
    memberById,
    normalizeState,
    drawMember,
    recruit,
    teamPower,
    memberPower,
    signCandidate,
    trainMember,
    performanceRank,
    resolvePerformance,
    signInStatus,
    claimDailySignIn,
    dailyTaskStatus,
    claimDailyTask,
    createBackup,
    restoreBackup
  };
})();

if (typeof module !== 'undefined' && module.exports) module.exports = ChoSirenCore;
