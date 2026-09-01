const {
  MEMBERS,
  INVENTORY_ITEMS,
  MAX_STAMINA,
  PERFORMANCE_STAMINA_COST,
  memberById,
  normalizeState,
  recruit,
  teamPower: calculateTeamPower,
  memberPower: calculateMemberPower,
  signCandidate,
  trainMember,
  resolvePerformance,
  signInStatus,
  claimDailySignIn,
  dailyTaskStatus,
  claimDailyTask,
  createBackup,
  restoreBackup
} = ChoSirenCore;
const STORAGE_KEY = 'cho-siren-save-v1';
const PREFERENCES_KEY = 'cho-siren-preferences-v1';
const FEATURE_STATE_KEY = 'cho-siren-feature-state-v1';
const DEFAULT_PREFERENCES = { motionEnabled: true, reducedEffects: false };
const DEFAULT_FEATURE_STATE = { playerName: '音律少女', claimedMailIds: [], liveRewardDay: '', reputation: 50, morale: 70, agencyDecision: '' };
const loadState = () => {
  try {
    return normalizeState(JSON.parse(localStorage.getItem(STORAGE_KEY)));
  } catch {
    return normalizeState();
  }
};

const loadPreferences = () => {
  try {
    const saved = JSON.parse(localStorage.getItem(PREFERENCES_KEY));
    return {
      motionEnabled: saved?.motionEnabled !== false,
      reducedEffects: Boolean(saved?.reducedEffects)
    };
  } catch {
    return { ...DEFAULT_PREFERENCES };
  }
};

let state = loadState();
let preferences = loadPreferences();
let rosterFilter = '全部';
let inventoryFilter = '全部';
let sortDescending = true;
let inventorySortDescending = true;
let newlyUnlocked = new Set();
let auditionCandidateIds = [];
let selectedCandidateId;
let interviewedCandidateIds = new Set();

const loadFeatureState = () => {
  try {
    const saved = JSON.parse(localStorage.getItem(FEATURE_STATE_KEY));
    return {
      playerName: typeof saved?.playerName === 'string' && saved.playerName.trim() ? saved.playerName.trim().slice(0, 12) : DEFAULT_FEATURE_STATE.playerName,
      claimedMailIds: Array.isArray(saved?.claimedMailIds) ? [...new Set(saved.claimedMailIds.filter(id => typeof id === 'string'))].slice(0, 20) : [],
      liveRewardDay: typeof saved?.liveRewardDay === 'string' ? saved.liveRewardDay : '',
      reputation: Number.isFinite(saved?.reputation) ? Math.max(0, Math.min(100, Math.floor(saved.reputation))) : DEFAULT_FEATURE_STATE.reputation,
      morale: Number.isFinite(saved?.morale) ? Math.max(0, Math.min(100, Math.floor(saved.morale))) : DEFAULT_FEATURE_STATE.morale,
      agencyDecision: ['invest', 'organic'].includes(saved?.agencyDecision) ? saved.agencyDecision : ''
    };
  } catch {
    return { ...DEFAULT_FEATURE_STATE };
  }
};

let featureState = loadFeatureState();

const saveFeatureState = () => {
  try {
    localStorage.setItem(FEATURE_STATE_KEY, JSON.stringify(featureState));
    return true;
  } catch {
    return false;
  }
};

const savePreferences = () => {
  try {
    localStorage.setItem(PREFERENCES_KEY, JSON.stringify(preferences));
    return true;
  } catch {
    return false;
  }
};

const applyPreferences = (notify = false) => {
  document.documentElement.classList.toggle('motion-disabled', !preferences.motionEnabled);
  document.documentElement.classList.toggle('reduced-effects', preferences.reducedEffects);
  if (notify) window.dispatchEvent(new CustomEvent('cho-siren-preferences'));
};
applyPreferences();

const saveState = () => {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
    return true;
  } catch {
    return false;
  }
};
const formatNumber = value => new Intl.NumberFormat('zh-CN').format(value);
const escapeHtml = value => String(value).replace(/[&<>'"]/g, character => ({
  '&': '&amp;', '<': '&lt;', '>': '&gt;', "'": '&#39;', '"': '&quot;'
}[character]));
const memberIndex = id => MEMBERS.findIndex(member => member.id === id);
const isOwned = id => (state.owned[id] || 0) > 0;
const talentBarsMarkup = member => [
  ['声乐', member.vocal],
  ['舞蹈', member.dance],
  ['魅力', member.charm],
  ['自律', member.discipline],
  ['风险', member.risk]
].map(([label, value]) => `<span class="talent-row${label === '风险' ? ' risk' : ''}"><small>${label}</small><i><u style="width:${value}%"></u></i><b>${value}</b></span>`).join('');
const normalizeTeam = () => {
  state = normalizeState(state);
};
normalizeTeam();

const toast = document.querySelector('.toast');
let toastTimer;
const showToast = message => {
  toast.textContent = message;
  toast.classList.add('show');
  clearTimeout(toastTimer);
  toastTimer = setTimeout(() => toast.classList.remove('show'), 1800);
};

const memberCard = member => {
  const count = state.owned[member.id] || 0;
  const locked = count === 0;
  const leader = state.leaderId === member.id;
  const index = memberIndex(member.id);
  const trainingLevel = state.training[member.id] || 0;
  return `<button type="button" class="member-card${locked ? ' locked' : ''}${leader ? ' is-leader' : ''}${newlyUnlocked.has(member.id) ? ' just-unlocked' : ''}" data-member-id="${member.id}" style="--n:${index};--member-art:url('${member.image}')" aria-disabled="${locked}" aria-pressed="${leader}" aria-label="${locked ? `未解锁${member.role}成员，点击查看获取提示` : `${member.name}，${member.role}，${member.rarity}，等级${member.level}${leader ? '，当前队长' : ''}`} ">
    <div class="art"></div>
    <div class="meta">
      <small>${member.role} · ${member.rarity}</small>
      <b>${locked ? '未解锁' : member.name}</b>
      <em>${locked ? '等待签约' : `Lv.${member.level + trainingLevel}`}</em>
      ${trainingLevel ? `<span class="training-badge">培养 +${trainingLevel}</span>` : ''}
      ${count > 1 ? `<span class="dupe-count">×${count}</span>` : ''}
    </div>
  </button>`;
};

const renderEconomy = () => {
  state = normalizeState(state);
  const formatted = formatNumber(state.gems);
  document.querySelector('#gemBalance').textContent = formatted;
  document.querySelector('#gemButton').setAttribute('aria-label', `声晶 ${formatted}，点击查看用途`);
  const coinText = `${formatNumber(state.coins)} K`;
  document.querySelector('#coinBalance').textContent = coinText;
  document.querySelector('#coinButton').setAttribute('aria-label', `星币 ${coinText}，点击查看用途`);
  document.querySelector('#staminaBalance').textContent = `${state.stamina}/${MAX_STAMINA}`;
  document.querySelector('#staminaBar').style.width = `${Math.round(state.stamina / MAX_STAMINA * 100)}%`;
  document.querySelector('#staminaButton').setAttribute('aria-label', state.stamina >= MAX_STAMINA
    ? '体力已满，每次演出消耗 10 点'
    : '每 5 分钟恢复 1 点体力');
};

const renderProfile = () => {
  document.querySelector('#profileButton b').textContent = featureState.playerName;
};

const renderMembers = () => {
  normalizeTeam();
  const ownedMembers = MEMBERS.filter(member => isOwned(member.id)).sort((a, b) => calculateMemberPower(state, b.id) - calculateMemberPower(state, a.id));
  const orderedTeamIds = [state.leaderId, ...state.team.filter(id => id !== state.leaderId)];
  const teamMembers = orderedTeamIds.map(memberById).filter(Boolean);
  document.querySelector('#memberGrid').innerHTML = teamMembers.map(memberCard).join('');
  const teamPower = teamMembers.reduce((total, member) => total + calculateMemberPower(state, member.id), 0);
  document.querySelector('#teamPower').textContent = formatNumber(teamPower);
  document.querySelector('#teamPowerBar').style.width = `${Math.min(100, Math.round(teamPower / 800))}%`;

  const visibleMembers = MEMBERS
    .filter(member => rosterFilter === '全部' || member.role === rosterFilter)
    .sort((a, b) => sortDescending ? calculateMemberPower(state, b.id) - calculateMemberPower(state, a.id) : calculateMemberPower(state, a.id) - calculateMemberPower(state, b.id));
  document.querySelector('#rosterGrid').innerHTML = visibleMembers.map(memberCard).join('');
  document.querySelector('#collectionProgress').textContent = `已拥有 ${ownedMembers.length} / ${MEMBERS.length} · 当前显示 ${visibleMembers.length} 名`;
  document.querySelector('#sortBtn').textContent = sortDescending ? '战力 ↓' : '战力 ↑';
};

const renderInventory = () => {
  const rarityRank = { SSR: 2, SR: 1, R: 0 };
  const visibleItems = INVENTORY_ITEMS.map((item, index) => ({ ...item, index }))
    .filter(item => inventoryFilter === '全部' || item.type === inventoryFilter)
    .sort((a, b) => (rarityRank[b.rarity] - rarityRank[a.rarity] || b.count - a.count) * (inventorySortDescending ? 1 : -1));
  document.querySelector('#inventoryGrid').innerHTML = visibleItems.map(item => `<button class="item" data-item-index="${item.index}" aria-label="查看${item.rarity}${item.type}，数量${item.count}"><em>${item.rarity}</em>${item.symbol}<small>×${item.count}</small></button>`).join('');
  document.querySelector('#inventorySortBtn').textContent = inventorySortDescending ? '级别 ↓' : '级别 ↑';
};

const shuffledMembers = members => [...members].sort(() => Math.random() - .5);

const chooseAuditionCandidates = () => {
  const unowned = shuffledMembers(MEMBERS.filter(member => !isOwned(member.id)));
  const owned = shuffledMembers(MEMBERS.filter(member => isOwned(member.id)));
  auditionCandidateIds = [...unowned, ...owned].slice(0, 3).map(member => member.id);
  selectedCandidateId = undefined;
  interviewedCandidateIds = new Set();
};

const renderAudition = () => {
  if (!auditionCandidateIds.length) chooseAuditionCandidates();
  const selected = memberById(selectedCandidateId);
  document.querySelector('#auditionCandidates').innerHTML = auditionCandidateIds.map((id, index) => {
    const member = memberById(id);
    const selectedClass = id === selectedCandidateId ? ' selected' : '';
    const signed = isOwned(id);
    return `<button class="audition-candidate${selectedClass}" data-candidate-id="${id}" style="--member-art:url('${member.image}')" aria-pressed="${id === selectedCandidateId}">
      <span class="candidate-art" aria-hidden="true"></span><small>候选 ${String(index + 1).padStart(2, '0')} · ${member.rarity}</small><b>${member.name}</b><em>${member.role}${signed ? ' · 已签约' : ''}</em>
    </button>`;
  }).join('');

  const title = document.querySelector('#candidateInsightTitle');
  const copy = document.querySelector('#candidateInsightCopy');
  const bars = document.querySelector('#candidateBars');
  const interviewButton = document.querySelector('#interviewCandidateBtn');
  const signButton = document.querySelector('#signCandidateBtn');
  if (!selected) {
    title.textContent = '请选择一位候选人';
    copy.textContent = '点击角色卡后进行面试，可查看完整能力与个性。';
    bars.innerHTML = '';
    interviewButton.disabled = true;
    signButton.disabled = true;
    return;
  }

  const interviewed = interviewedCandidateIds.has(selected.id);
  title.textContent = `${selected.name} · ${selected.role}`;
  copy.textContent = interviewed
    ? `${selected.personality}，最看重“${selected.value}”。${isOwned(selected.id) ? '再次签约将提升共鸣。' : '尚未签约，可纳入公司培养。'}`
    : '档案尚未解锁。进行一次面试，了解她的能力、价值观与公关风险。';
  bars.innerHTML = interviewed ? talentBarsMarkup(selected) : '<span class="candidate-locked">面试后显示完整能力报告</span>';
  interviewButton.disabled = interviewed;
  interviewButton.innerHTML = interviewed ? '面试完成<small>档案已解锁</small>' : '进行面试<small>查看完整档案</small>';
  signButton.disabled = !interviewed;
};

const renderLobbyProgress = () => {
  const task = dailyTaskStatus(state);
  const dailyButton = document.querySelector('#dailyTaskButton');
  document.querySelector('#dailyTaskProgress').textContent = `${task.progress} / ${task.target}`;
  document.querySelector('#dailyTaskBar').style.width = `${Math.round(task.progress / task.target * 100)}%`;
  document.querySelector('#dailyTaskBadge').textContent = task.claimed ? '✓' : task.ready ? '!' : '○';
  document.querySelector('#dailyTaskCopy').textContent = task.claimed
    ? '今日奖励已领取'
    : task.ready ? '任务完成，点击领取' : `完成 ${task.target} 次演出`;
  dailyButton.classList.toggle('reward-ready', task.ready);
  dailyButton.classList.toggle('reward-claimed', task.claimed);
  dailyButton.setAttribute('aria-label', task.claimed
    ? '今日演出任务奖励已领取'
    : task.ready ? `领取每日任务奖励，声晶 ${task.reward.gems}，星币 ${task.reward.coins}K` : `每日演出任务，已完成 ${task.progress} 次，共 ${task.target} 次`);

  const signIn = signInStatus(state);
  const signInButton = document.querySelector('#signInButton');
  document.querySelector('#signInLabel').textContent = signIn.claimed ? '今日已签到' : '每日签到';
  document.querySelector('#signInStreak').textContent = `连续第 ${signIn.claimed ? state.signInStreak : signIn.nextStreak} 天`;
  document.querySelector('#signInReward').textContent = signIn.claimed ? '明日再来' : `◇ ×${signIn.reward}`;
  signInButton.classList.toggle('reward-ready', !signIn.claimed);
  signInButton.classList.toggle('reward-claimed', signIn.claimed);
  signInButton.setAttribute('aria-label', signIn.claimed ? '今日签到奖励已领取' : `领取今日签到奖励，声晶 ${signIn.reward}`);
};

const renderAll = () => {
  renderProfile();
  renderEconomy();
  renderMembers();
  renderInventory();
  renderAudition();
  renderLobbyProgress();
};

const screens = [...document.querySelectorAll('.screen')];
const navButtons = [...document.querySelectorAll('[data-screen]')];
const updateNavAccessibility = activeButton => navButtons.forEach(item => {
  const active = item === activeButton;
  item.classList.toggle('active', active);
  if (active) item.setAttribute('aria-current', 'page');
  else item.removeAttribute('aria-current');
});
navButtons.forEach(button => button.addEventListener('click', () => {
  screens.forEach(screen => screen.classList.toggle('active', screen.id === button.dataset.screen));
  updateNavAccessibility(button);
}));
updateNavAccessibility(navButtons.find(button => button.classList.contains('active')));

const openModal = dialog => {
  if (!dialog || dialog.open) return;
  try {
    if (typeof dialog.showModal === 'function') dialog.showModal();
    else dialog.setAttribute('open', '');
  } catch {
    dialog.setAttribute('open', '');
  }
};

const closeModal = dialog => {
  if (!dialog?.open) return;
  if (typeof dialog.close === 'function') dialog.close();
  else dialog.removeAttribute('open');
};

const activateScreen = screenId => {
  const button = navButtons.find(item => item.dataset.screen === screenId);
  if (button) button.click();
};

const featureDialog = document.querySelector('#featureDialog');
const featureDialogContent = document.querySelector('#featureDialogContent');
const featureDialogActions = document.querySelector('#featureDialogActions');
const MAILS = [
  { id: 'launch-gift', title: '幻域启程礼', copy: '感谢制作人加入 CHO-SIREN。', gems: 300, coins: 1200 },
  { id: 'stage-support', title: '舞台应援回礼', copy: '今日直播间热度突破 20 万。', gems: 80, coins: 500 },
  { id: 'maintenance', title: '维护补偿', copy: '设置与成员界面现已完成优化。', gems: 120, coins: 800 }
];

const openFeatureDialog = ({ eyebrow = 'CHO-SIREN', title, content, actions = '' }) => {
  document.querySelector('#featureDialogEyebrow').textContent = eyebrow;
  document.querySelector('#featureDialogTitle').textContent = title;
  featureDialogContent.innerHTML = content;
  featureDialogActions.innerHTML = actions || '<button class="dialog-confirm" data-feature-action="close">关闭</button>';
  openModal(featureDialog);
};

const openProfilePanel = () => {
  const ownedCount = MEMBERS.filter(member => isOwned(member.id)).length;
  openFeatureDialog({
    eyebrow: 'PRODUCER PROFILE',
    title: '制作人档案',
    content: `<div class="profile-summary"><span class="avatar profile-avatar" aria-hidden="true">♪</span><div><b>${escapeHtml(featureState.playerName)}</b><small>Lv.68 · 新星制作人</small></div></div>
      <div class="feature-stats"><span>团队战力<b>${formatNumber(calculateTeamPower(state))}</b></span><span>已拥有成员<b>${ownedCount} / ${MEMBERS.length}</b></span><span>公司口碑<b>${featureState.reputation}</b></span><span>团队士气<b>${featureState.morale}</b></span><span>累计演出<b>${formatNumber(state.performanceCount)}</b></span><span>最高分<b>${formatNumber(state.bestScore)}</b></span></div>
      <label class="feature-field" for="profileNameInput"><span>制作人昵称</span><input id="profileNameInput" maxlength="12" value="${escapeHtml(featureState.playerName)}" /></label>`,
    actions: '<button class="dialog-confirm" data-feature-action="save-profile">保存昵称</button>'
  });
};

const openMailPanel = () => {
  const unclaimed = MAILS.filter(mail => !featureState.claimedMailIds.includes(mail.id)).length;
  const rows = MAILS.map(mail => {
    const claimed = featureState.claimedMailIds.includes(mail.id);
    return `<article class="feature-row${claimed ? ' is-complete' : ''}"><div><b>${mail.title}</b><p>${mail.copy}</p><small>◇ ${mail.gems}　B ${formatNumber(mail.coins)} K</small></div><button ${claimed ? 'disabled' : ''} data-feature-action="claim-mail" data-mail-id="${mail.id}">${claimed ? '已领取' : '领取'}</button></article>`;
  }).join('');
  openFeatureDialog({ eyebrow: 'INBOX', title: `邮件 · ${unclaimed} 封未领取`, content: `<div class="feature-list">${rows}</div>` });
};

const openNoticePanel = () => openFeatureDialog({
  eyebrow: 'NEWS & SOUND',
  title: '公告与音效',
  content: `<article class="notice-card"><small>版本 1.2.0</small><b>舞台体验更新</b><p>设置按钮、成员立绘和功能入口已更新。页面切换后角色动画会自动恢复。</p></article>
    <article class="notice-card"><small>活动进行中</small><b>闪耀舞台计划</b><p>完成演出可获得声晶、星币并推进每日任务。</p></article>`,
  actions: '<button data-feature-action="play-cue">试听舞台提示音</button><button class="dialog-confirm" data-feature-action="close">知道了</button>'
});

const openLivePanel = () => {
  const claimed = featureState.liveRewardDay === ChoSirenCore.dayKey();
  openFeatureDialog({
    eyebrow: 'LIVE ROOM',
    title: '星夜直播间',
    content: `<div class="live-room-preview"><span>♡⌁♡</span><b>203,842</b><small>名制作人正在观看</small></div><p class="feature-copy">发送一次今日应援，可获得声晶 30 与星币 200K。每天可领取一次。</p>`,
    actions: claimed
      ? '<button class="dialog-confirm" disabled>今日已应援</button>'
      : '<button class="dialog-confirm" data-feature-action="support-live">发送今日应援</button>'
  });
};

const openEventPanel = () => {
  const decisionCopy = featureState.agencyDecision === 'invest'
    ? '你批准了城市宣传预算，团队士气与公司口碑明显提升。'
    : featureState.agencyDecision === 'organic'
      ? '你选择靠成员自然发酵话题，保住预算，但团队压力有所上升。'
      : '品牌方愿意把“闪耀舞台计划”交给公司，但需要立刻追加 3,000K 宣传预算。你必须作出经营决定。';
  openFeatureDialog({
    eyebrow: 'AGENCY DECISION',
    title: '闪耀舞台计划',
    content: `<div class="event-progress"><span>公司口碑 <b>${featureState.reputation}</b></span><i><u style="width:${featureState.reputation}%"></u></i></div><div class="event-progress"><span>团队士气 <b>${featureState.morale}</b></span><i><u style="width:${featureState.morale}%"></u></i></div><p class="feature-copy">${decisionCopy}</p>`,
    actions: featureState.agencyDecision
      ? '<button class="dialog-confirm" data-feature-action="start-event">进入活动关卡</button>'
      : '<button data-feature-action="event-organic">自然运营 · 不花钱</button><button class="dialog-confirm" data-feature-action="event-invest">追加宣传 · 3,000K</button>'
  });
};

const openChapterMap = () => {
  const unlocked = Math.min(6, 3 + Math.floor(state.performanceCount / 2));
  const stageNames = ['初次试镜', '城市路演', '霓虹街区', '舆情风波', '品牌晚宴', '月曜决选'];
  const nodes = stageNames.map((name, index) => {
    const number = index + 1;
    const locked = number > unlocked;
    const completed = number < unlocked;
    return `<button class="chapter-node${completed ? ' completed' : ''}${number === unlocked ? ' current' : ''}" ${locked ? 'disabled' : ''} data-feature-action="start-stage" data-stage="7-${number}"><small>7-${number}</small><b>${name}</b><em>${locked ? '未解锁' : completed ? '已完成' : '当前关卡'}</em></button>`;
  }).join('');
  openFeatureDialog({
    eyebrow: 'CHAPTER 07 · CITY TOUR',
    title: '踏梦迷踪 · 关卡地图',
    content: `<div class="chapter-map">${nodes}</div><p class="feature-copy">演出次数会推进城市巡演路线。当前已解锁至 7-${unlocked}，每完成两场演出解锁下一站。</p>`
  });
};

const openResourcePanel = type => {
  const details = {
    gems: ['声晶', `当前拥有 ${formatNumber(state.gems)}`, '用于成员共鸣招募。演出、签到、每日任务和邮件都能获得。', 'go-recruit', '前往招募'],
    coins: ['星币', `当前拥有 ${formatNumber(state.coins)} K`, '用于养成成员与饰品。当前版本可查看库存，后续养成会消耗星币。', 'go-inventory', '查看饰品'],
    stamina: ['体力', `当前 ${state.stamina}/${MAX_STAMINA}`, `每次演出消耗 ${PERFORMANCE_STAMINA_COST} 点，每 5 分钟恢复 1 点。`, 'start-live', '开始演出']
  }[type];
  openFeatureDialog({
    eyebrow: 'RESOURCE',
    title: details[0],
    content: `<div class="resource-balance">${details[1]}</div><p class="feature-copy">${details[2]}</p>`,
    actions: `<button class="dialog-confirm" data-feature-action="${details[3]}">${details[4]}</button>`
  });
};

const playStageCue = () => {
  const AudioContextClass = window.AudioContext || window.webkitAudioContext;
  if (!AudioContextClass) {
    showToast('当前浏览器不支持提示音');
    return;
  }
  const context = new AudioContextClass();
  const gain = context.createGain();
  gain.gain.setValueAtTime(.0001, context.currentTime);
  gain.gain.exponentialRampToValueAtTime(.12, context.currentTime + .02);
  gain.gain.exponentialRampToValueAtTime(.0001, context.currentTime + .55);
  gain.connect(context.destination);
  [523.25, 659.25, 783.99].forEach((frequency, index) => {
    const oscillator = context.createOscillator();
    oscillator.type = 'sine';
    oscillator.frequency.value = frequency;
    oscillator.connect(gain);
    oscillator.start(context.currentTime + index * .07);
    oscillator.stop(context.currentTime + .6);
  });
  setTimeout(() => context.close(), 800);
  showToast('舞台提示音已播放');
};

document.querySelector('#profileButton').addEventListener('click', openProfilePanel);
document.querySelector('#mailButton').addEventListener('click', openMailPanel);
document.querySelector('#noticeButton').addEventListener('click', openNoticePanel);
document.querySelector('#liveButton').addEventListener('click', openLivePanel);
document.querySelector('#eventButton').addEventListener('click', openEventPanel);
document.querySelector('#shopButton').addEventListener('click', () => {
  activateScreen('recruit');
  showToast('已进入限时共鸣招募');
});
document.querySelector('#gemButton').addEventListener('click', () => openResourcePanel('gems'));
document.querySelector('#coinButton').addEventListener('click', () => openResourcePanel('coins'));
document.querySelector('#staminaButton').addEventListener('click', () => openResourcePanel('stamina'));
document.querySelector('#inventorySortBtn').addEventListener('click', () => {
  inventorySortDescending = !inventorySortDescending;
  renderInventory();
});

document.querySelector('#inventoryGrid').addEventListener('click', event => {
  const itemButton = event.target.closest('[data-item-index]');
  if (!itemButton) return;
  const item = INVENTORY_ITEMS[Number(itemButton.dataset.itemIndex)];
  if (!item) return;
  openFeatureDialog({
    eyebrow: 'ITEM DETAIL',
    title: `${item.rarity} · ${item.type}`,
    content: `<div class="item-preview" aria-hidden="true">${item.symbol}</div><div class="feature-stats"><span>持有数量<b>×${item.count}</b></span><span>稀有度<b>${item.rarity}</b></span></div><p class="feature-copy">可在后续成员养成中装备；当前库存和筛选结果会实时更新。</p>`,
    actions: '<button class="dialog-confirm" data-feature-action="go-team">查看团队</button>'
  });
});

document.querySelector('#auditionCandidates').addEventListener('click', event => {
  const candidate = event.target.closest('[data-candidate-id]');
  if (!candidate) return;
  selectedCandidateId = candidate.dataset.candidateId;
  renderAudition();
});

document.querySelector('#refreshCandidatesBtn').addEventListener('click', () => {
  chooseAuditionCandidates();
  renderAudition();
  showToast('星探已带回一批新候选人');
});

document.querySelector('#interviewCandidateBtn').addEventListener('click', () => {
  if (!selectedCandidateId) return;
  interviewedCandidateIds.add(selectedCandidateId);
  renderAudition();
  showToast(`${memberById(selectedCandidateId).name}的完整面试档案已解锁`);
});

document.querySelector('#signCandidateBtn').addEventListener('click', () => {
  if (!selectedCandidateId || !interviewedCandidateIds.has(selectedCandidateId)) return;
  const outcome = signCandidate(state, selectedCandidateId, 150);
  if (outcome.error === 'INSUFFICIENT_GEMS') {
    showToast(`声晶不足，还需要 ${formatNumber(outcome.missing)}`);
    return;
  }
  state = outcome.state;
  if (outcome.isNew) newlyUnlocked.add(outcome.member.id);
  saveState();
  const signedResult = resultCard({ member: outcome.member, isNew: outcome.isNew, ownedCount: outcome.ownedCount });
  chooseAuditionCandidates();
  renderAll();
  openRecruitDialog(outcome.isNew ? '签约成功' : '共鸣提升', signedResult, '欢迎加入公司');
});

document.querySelector('#closeFeatureDialog').addEventListener('click', () => closeModal(featureDialog));
featureDialog.addEventListener('cancel', event => {
  event.preventDefault();
  closeModal(featureDialog);
});

featureDialog.addEventListener('click', event => {
  const actionButton = event.target.closest('[data-feature-action]');
  if (!actionButton || actionButton.disabled) return;
  const action = actionButton.dataset.featureAction;
  if (action === 'close') closeModal(featureDialog);
  if (action === 'save-profile') {
    const input = document.querySelector('#profileNameInput');
    const nextName = input?.value.trim().slice(0, 12);
    if (!nextName) return showToast('昵称不能为空');
    featureState.playerName = nextName;
    saveFeatureState();
    renderProfile();
    closeModal(featureDialog);
    showToast('制作人昵称已保存');
  }
  if (action === 'claim-mail') {
    const mail = MAILS.find(item => item.id === actionButton.dataset.mailId);
    if (!mail || featureState.claimedMailIds.includes(mail.id)) return;
    state.gems += mail.gems;
    state.coins += mail.coins;
    featureState.claimedMailIds.push(mail.id);
    saveState();
    saveFeatureState();
    renderAll();
    openMailPanel();
    showToast(`已领取：声晶 ×${mail.gems}，星币 ×${mail.coins}K`);
  }
  if (action === 'support-live') {
    const today = ChoSirenCore.dayKey();
    if (featureState.liveRewardDay === today) return;
    featureState.liveRewardDay = today;
    state.gems += 30;
    state.coins += 200;
    saveFeatureState();
    saveState();
    renderAll();
    openLivePanel();
    showToast('应援成功：声晶 ×30，星币 ×200K');
  }
  if (action === 'play-cue') playStageCue();
  if (action === 'event-invest') {
    if (state.coins < 3000) {
      showToast(`星币不足，还需要 ${formatNumber(3000 - state.coins)}K`);
      return;
    }
    state.coins -= 3000;
    featureState.reputation = Math.min(100, featureState.reputation + 8);
    featureState.morale = Math.min(100, featureState.morale + 6);
    featureState.agencyDecision = 'invest';
    saveState();
    saveFeatureState();
    renderAll();
    openEventPanel();
    showToast('宣传计划通过：口碑 +8，士气 +6');
  }
  if (action === 'event-organic') {
    featureState.reputation = Math.min(100, featureState.reputation + 2);
    featureState.morale = Math.max(0, featureState.morale - 4);
    featureState.agencyDecision = 'organic';
    saveFeatureState();
    openEventPanel();
    showToast('选择自然运营：口碑 +2，士气 -4');
  }
  if (action === 'go-recruit') { closeModal(featureDialog); activateScreen('recruit'); }
  if (action === 'go-inventory') { closeModal(featureDialog); activateScreen('accessory'); }
  if (action === 'go-team') { closeModal(featureDialog); activateScreen('team'); }
  if (action === 'start-live') { closeModal(featureDialog); openPerformanceDialog('live'); }
  if (action === 'start-event') { openChapterMap(); }
  if (action === 'start-stage') {
    const stage = actionButton.dataset.stage || '7-1';
    closeModal(featureDialog);
    openPerformanceDialog('mission', stage);
  }
});

document.querySelectorAll('[data-filter-group]').forEach(row => row.addEventListener('click', event => {
  const button = event.target.closest('button[data-filter]');
  if (!button) return;
  row.querySelectorAll('button').forEach(item => {
    const active = item === button;
    item.classList.toggle('active', active);
    item.setAttribute('aria-pressed', String(active));
  });
  if (row.dataset.filterGroup === 'roster') {
    rosterFilter = button.dataset.filter;
    renderMembers();
  } else {
    inventoryFilter = button.dataset.filter;
    renderInventory();
  }
}));
document.querySelectorAll('[data-filter-group] button').forEach(button => button.setAttribute('aria-pressed', String(button.classList.contains('active'))));

document.querySelector('#sortBtn').addEventListener('click', () => {
  sortDescending = !sortDescending;
  renderMembers();
});

document.querySelector('#signInButton').addEventListener('click', () => {
  const outcome = claimDailySignIn(state);
  if (outcome.error === 'ALREADY_CLAIMED') {
    showToast('今日签到奖励已经领取，明天再来');
    return;
  }
  state = outcome.state;
  saveState();
  renderAll();
  showToast(`签到成功：声晶 ×${outcome.reward}，连续 ${state.signInStreak} 天`);
});

document.querySelector('#dailyTaskButton').addEventListener('click', () => {
  const outcome = claimDailyTask(state);
  if (outcome.error === 'ALREADY_CLAIMED') {
    showToast('今日任务奖励已经领取');
    return;
  }
  if (outcome.error === 'NOT_READY') {
    showToast(`再完成 ${outcome.missing} 次演出即可领取`);
    return;
  }
  state = outcome.state;
  saveState();
  renderAll();
  showToast(`每日任务完成：声晶 ×${outcome.reward.gems}，星币 ×${outcome.reward.coins}K`);
});

const memberDialog = document.querySelector('#memberDialog');
const setLeaderButton = document.querySelector('#setLeaderBtn');
const trainMemberButton = document.querySelector('#trainMemberBtn');
let selectedMemberId;

const openMemberDialog = member => {
  selectedMemberId = member.id;
  document.querySelector('#memberDialogTitle').textContent = member.name;
  document.querySelector('#memberDetailArt').style.setProperty('--member-art', `url('${member.image}')`);
  document.querySelector('#memberDetailRole').textContent = `${member.rarity} · ${member.role}`;
  document.querySelector('#memberDetailSummary').textContent = `${member.name}擅长${member.role === '主唱' ? '稳定舞台情绪与高音爆发' : member.role === '舞者' ? '节奏连击与舞台表现' : '团队增益与共鸣恢复'}。`;
  document.querySelector('#memberDetailPersonality').textContent = `个性：${member.personality}　｜　价值观：${member.value}`;
  document.querySelector('#memberTalentGrid').innerHTML = talentBarsMarkup(member);
  const trainingLevel = state.training[member.id] || 0;
  document.querySelector('#memberDetailLevel').textContent = `Lv.${member.level + trainingLevel}`;
  document.querySelector('#memberDetailPower').textContent = formatNumber(calculateMemberPower(state, member.id));
  document.querySelector('#memberDetailCopies').textContent = `×${state.owned[member.id] || 0}`;
  const isLeader = state.leaderId === member.id;
  setLeaderButton.disabled = isLeader;
  setLeaderButton.textContent = isLeader ? '当前队长' : '设为队长';
  trainMemberButton.disabled = trainingLevel >= 20;
  trainMemberButton.textContent = trainingLevel >= 20 ? '培养已达到上限' : `专项培养 +1 · 星币 600K`;
  openModal(memberDialog);
};

document.addEventListener('click', event => {
  const card = event.target.closest('[data-member-id]');
  if (!card) return;
  const member = memberById(card.dataset.memberId);
  if (!member) return;
  if (!isOwned(member.id)) {
    showToast(`${member.role}成员尚未解锁，请前往招募`);
    return;
  }
  openMemberDialog(member);
});

document.querySelector('#autoTeamBtn').addEventListener('click', () => {
  state.team = MEMBERS.filter(member => isOwned(member.id)).sort((a, b) => calculateMemberPower(state, b.id) - calculateMemberPower(state, a.id)).slice(0, 4).map(member => member.id);
  state.leaderId = state.team[0];
  saveState();
  renderMembers();
  showToast(`自动编队完成，战力 ${document.querySelector('#teamPower').textContent}`);
});

setLeaderButton.addEventListener('click', () => {
  if (!selectedMemberId || !isOwned(selectedMemberId)) return;
  state.team = [selectedMemberId, ...state.team.filter(id => id !== selectedMemberId)].slice(0, 4);
  state.leaderId = selectedMemberId;
  normalizeTeam();
  saveState();
  renderMembers();
  openMemberDialog(memberById(selectedMemberId));
  showToast(`${memberById(selectedMemberId).name}已设为队长`);
});

trainMemberButton.addEventListener('click', () => {
  if (!selectedMemberId) return;
  const outcome = trainMember(state, selectedMemberId, 600);
  if (outcome.error === 'INSUFFICIENT_COINS') {
    showToast(`星币不足，还需要 ${formatNumber(outcome.missing)}K`);
    return;
  }
  if (outcome.error === 'MAX_TRAINING') {
    showToast('该成员已经完成全部培养');
    return;
  }
  if (outcome.error) return;
  state = outcome.state;
  saveState();
  renderAll();
  openMemberDialog(outcome.member);
  showToast(`${outcome.member.name}培养完成，战力 +${outcome.powerGain}`);
});

const closeMemberDialog = () => closeModal(memberDialog);
document.querySelector('#closeMemberDialog').addEventListener('click', closeMemberDialog);
memberDialog.addEventListener('cancel', event => {
  event.preventDefault();
  closeMemberDialog();
});

const recruitDialog = document.querySelector('#recruitDialog');
const recruitResults = document.querySelector('#recruitResults');
const recruitDialogTitle = document.querySelector('#recruitDialogTitle');
const confirmRecruit = document.querySelector('#confirmRecruit');

const resultCard = ({ member, isNew, ownedCount }) => `<article class="recruit-result-card rarity-${member.rarity.toLowerCase()}" style="--member-art:url('${member.image}')">
  <div class="result-art"></div>
  <small>${member.rarity} · ${member.role}</small>
  <b>${member.name}</b>
  <em>${isNew ? 'NEW' : `共鸣 ×${ownedCount}`}</em>
</article>`;

const openRecruitDialog = (title, content, confirmLabel = '收下成员') => {
  recruitDialogTitle.textContent = title;
  recruitResults.innerHTML = content;
  recruitResults.classList.toggle('single', recruitResults.children.length === 1);
  confirmRecruit.textContent = confirmLabel;
  openModal(recruitDialog);
};

const performRecruit = (count, cost) => {
  const outcome = recruit(state, count, cost);
  if (outcome.error === 'INSUFFICIENT_GEMS') {
    showToast(`声晶不足，还需要 ${formatNumber(outcome.missing)}`);
    return;
  }
  state = outcome.state;
  outcome.results.filter(result => result.isNew).forEach(result => newlyUnlocked.add(result.member.id));
  saveState();
  renderAll();
  openRecruitDialog(count === 10 ? '十连共鸣结果' : '共鸣招募结果', outcome.results.map(resultCard).join(''));
};

document.querySelectorAll('[data-recruit-count]').forEach(button => button.addEventListener('click', () => {
  performRecruit(Number(button.dataset.recruitCount), Number(button.dataset.recruitCost));
}));

document.querySelector('#recruitHistoryBtn').addEventListener('click', () => {
  if (!state.history.length) {
    openRecruitDialog('招募记录', '<p class="empty-history">还没有招募记录，进行第一次共鸣招募吧。</p>', '知道了');
    return;
  }
  const rows = state.history.map(entry => {
    const names = entry.members.map(id => MEMBERS.find(member => member.id === id)?.name).filter(Boolean).join('、');
    return `<article class="history-row"><small>${new Date(entry.at).toLocaleString('zh-CN')}</small><b>${entry.count} 次招募</b><span>${names}</span></article>`;
  }).join('');
  openRecruitDialog('招募记录', rows, '关闭记录');
});

const closeRecruitDialog = () => {
  closeModal(recruitDialog);
  newlyUnlocked = new Set();
  renderMembers();
};
document.querySelector('#closeRecruitDialog').addEventListener('click', closeRecruitDialog);
confirmRecruit.addEventListener('click', closeRecruitDialog);
recruitDialog.addEventListener('cancel', event => {
  event.preventDefault();
  closeRecruitDialog();
});

const PERFORMANCE_TOTAL_BEATS = 10;
const PERFORMANCE_BEAT_INTERVAL = 720;
const PERFORMANCE_HIT_WINDOW = 260;
const performanceDialog = document.querySelector('#performanceDialog');
const performancePlayfield = document.querySelector('#performancePlayfield');
const beatButton = document.querySelector('#beatButton');
const startPerformanceButton = document.querySelector('#startPerformanceBtn');
const performanceStatus = document.querySelector('#performanceStatus');
const beatProgress = document.querySelector('#beatProgress');
const hitCount = document.querySelector('#hitCount');
const liveJudgement = document.querySelector('#liveJudgement');
let performanceSession;
let performanceFrame;
let judgementTimer;
let activePerformanceMode = 'live';
let activePerformanceStage = '7-1';

const updatePerformanceSummary = () => {
  document.querySelector('#bestScore').textContent = formatNumber(state.bestScore);
  document.querySelector('#performanceCount').textContent = formatNumber(state.performanceCount);
};

const showJudgement = (label, className) => {
  liveJudgement.textContent = label;
  performancePlayfield.classList.remove('perfect', 'good', 'miss');
  performancePlayfield.classList.add(className);
  clearTimeout(judgementTimer);
  judgementTimer = setTimeout(() => performancePlayfield.classList.remove('perfect', 'good', 'miss'), 190);
};

const resetPerformanceControls = () => {
  cancelAnimationFrame(performanceFrame);
  clearTimeout(judgementTimer);
  performanceSession = undefined;
  performancePlayfield.classList.remove('playing', 'perfect', 'good', 'miss');
  performancePlayfield.style.setProperty('--beat-scale', '1.55');
  beatButton.disabled = true;
  beatButton.querySelector('b').textContent = '点击节拍';
  beatButton.querySelector('small').textContent = '等待演出开始';
  startPerformanceButton.disabled = false;
  startPerformanceButton.textContent = `开始演出 · 体力 -${PERFORMANCE_STAMINA_COST}`;
  beatProgress.textContent = `0 / ${PERFORMANCE_TOTAL_BEATS}`;
  hitCount.textContent = '0';
  liveJudgement.textContent = 'READY';
};

const finishPerformance = () => {
  if (!performanceSession) return;
  const completed = performanceSession;
  performanceSession = undefined;
  cancelAnimationFrame(performanceFrame);
  performancePlayfield.classList.remove('playing');
  performancePlayfield.style.setProperty('--beat-scale', '1.55');
  beatButton.disabled = true;
  beatButton.querySelector('small').textContent = '演出完成';
  const outcome = resolvePerformance(state, {
    quality: completed.quality,
    totalNotes: PERFORMANCE_TOTAL_BEATS,
    teamPower: calculateTeamPower(state)
  });
  if (outcome.error === 'INSUFFICIENT_STAMINA') {
    performanceStatus.textContent = `体力不足，还需要 ${outcome.missing} 点。`;
    startPerformanceButton.disabled = true;
    return;
  }
  state = outcome.state;
  saveState();
  renderAll();
  updatePerformanceSummary();
  const accuracy = Math.round(outcome.accuracy * 100);
  liveJudgement.textContent = `${outcome.rank} RANK`;
  performanceStatus.textContent = `${activePerformanceMode === 'mission' ? `${activePerformanceStage} 通关 · ` : ''}${outcome.rank} 级 · ${formatNumber(outcome.score)} 分 · 命中率 ${accuracy}%　奖励声晶 ×${outcome.rewards.gems}、星币 ×${outcome.rewards.coins}K`;
  startPerformanceButton.disabled = state.stamina < PERFORMANCE_STAMINA_COST;
  startPerformanceButton.textContent = state.stamina < PERFORMANCE_STAMINA_COST ? '体力不足' : `再演一场 · 体力 -${PERFORMANCE_STAMINA_COST}`;
};

const performanceLoop = now => {
  if (!performanceSession || performanceSession.pausedAt) return;
  let target = performanceSession.startAt + performanceSession.nextBeat * PERFORMANCE_BEAT_INTERVAL;
  while (performanceSession.nextBeat < PERFORMANCE_TOTAL_BEATS && now - target > PERFORMANCE_HIT_WINDOW) {
    performanceSession.nextBeat += 1;
    showJudgement('MISS', 'miss');
    target = performanceSession.startAt + performanceSession.nextBeat * PERFORMANCE_BEAT_INTERVAL;
  }

  beatProgress.textContent = `${performanceSession.nextBeat} / ${PERFORMANCE_TOTAL_BEATS}`;
  hitCount.textContent = String(performanceSession.hits);
  if (performanceSession.nextBeat >= PERFORMANCE_TOTAL_BEATS) {
    finishPerformance();
    return;
  }

  const distance = Math.min(PERFORMANCE_BEAT_INTERVAL, Math.abs(target - now));
  performancePlayfield.style.setProperty('--beat-scale', (1 + distance / PERFORMANCE_BEAT_INTERVAL * .55).toFixed(3));
  performanceFrame = requestAnimationFrame(performanceLoop);
};

beatButton.addEventListener('click', () => {
  if (!performanceSession || performanceSession.pausedAt) return;
  const now = performance.now();
  const target = performanceSession.startAt + performanceSession.nextBeat * PERFORMANCE_BEAT_INTERVAL;
  const delta = Math.abs(now - target);
  if (delta > PERFORMANCE_HIT_WINDOW) {
    performanceStatus.textContent = now < target ? '再等等，光环接近中心时点击。' : '这一拍错过了，跟住下一拍。';
    return;
  }

  let quality = .65;
  let label = 'GOOD';
  let className = 'good';
  if (delta <= 90) {
    quality = 1;
    label = 'PERFECT';
    className = 'perfect';
  } else if (delta <= 170) {
    quality = .85;
    label = 'GREAT';
  }
  performanceSession.quality += quality;
  performanceSession.hits += 1;
  performanceSession.nextBeat += 1;
  showJudgement(label, className);
  performanceStatus.textContent = `${label}！继续跟随收缩光环。`;
  beatProgress.textContent = `${performanceSession.nextBeat} / ${PERFORMANCE_TOTAL_BEATS}`;
  hitCount.textContent = String(performanceSession.hits);
  if (performanceSession.nextBeat >= PERFORMANCE_TOTAL_BEATS) finishPerformance();
});

startPerformanceButton.addEventListener('click', () => {
  state = normalizeState(state);
  renderEconomy();
  if (state.stamina < PERFORMANCE_STAMINA_COST) {
    performanceStatus.textContent = `体力不足，还需要 ${PERFORMANCE_STAMINA_COST - state.stamina} 点。`;
    startPerformanceButton.disabled = true;
    return;
  }
  resetPerformanceControls();
  performanceSession = {
    startAt: performance.now() + 700,
    nextBeat: 0,
    hits: 0,
    quality: 0,
    pausedAt: 0
  };
  performancePlayfield.classList.add('playing');
  beatButton.disabled = false;
  beatButton.querySelector('small').textContent = '光环到中心时点击';
  startPerformanceButton.disabled = true;
  startPerformanceButton.textContent = '演出进行中';
  performanceStatus.textContent = '准备第一拍……';
  beatButton.focus();
  performanceFrame = requestAnimationFrame(performanceLoop);
});

const openPerformanceDialog = (mode = 'live', stage = '7-1') => {
  state = normalizeState(state);
  activePerformanceMode = mode;
  activePerformanceStage = stage;
  renderEconomy();
  resetPerformanceControls();
  updatePerformanceSummary();
  const isMission = mode === 'mission';
  document.querySelector('#performanceDialogTitle').textContent = isMission ? `冒险剧本 · ${stage}` : '星途演出';
  document.querySelector('#performanceInstructions').textContent = isMission
    ? `${stage} 踏梦迷踪：光环收缩到中心时点击节拍。保持连击，完成本章舞台。`
    : '光环收缩到中心时点击节拍。共 10 拍，越接近中心评分越高。';
  performanceStatus.textContent = state.stamina < PERFORMANCE_STAMINA_COST
    ? `体力不足，还需要 ${PERFORMANCE_STAMINA_COST - state.stamina} 点。`
    : `${isMission ? '第 07 章挑战已就绪。' : '准备好后开始演出。'}消耗 ${PERFORMANCE_STAMINA_COST} 点体力。`;
  startPerformanceButton.disabled = state.stamina < PERFORMANCE_STAMINA_COST;
  startPerformanceButton.textContent = state.stamina < PERFORMANCE_STAMINA_COST ? '体力不足' : `开始演出 · 体力 -${PERFORMANCE_STAMINA_COST}`;
  setTimeout(() => openModal(performanceDialog), 0);
};

const closePerformanceDialog = () => {
  resetPerformanceControls();
  closeModal(performanceDialog);
};

document.querySelector('#stageButton').addEventListener('click', () => openPerformanceDialog('live'));
document.querySelector('#missionButton').addEventListener('click', openChapterMap);
document.querySelector('#closePerformanceDialog').addEventListener('click', closePerformanceDialog);
performanceDialog.addEventListener('cancel', event => {
  event.preventDefault();
  closePerformanceDialog();
});

document.addEventListener('visibilitychange', () => {
  if (!performanceSession) return;
  if (document.hidden) {
    performanceSession.pausedAt = performance.now();
    cancelAnimationFrame(performanceFrame);
    performanceStatus.textContent = '演出已暂停，返回页面后继续。';
    return;
  }
  if (performanceSession.pausedAt) {
    const resumedAt = performance.now();
    performanceSession.startAt += resumedAt - performanceSession.pausedAt;
    performanceSession.pausedAt = 0;
    performanceStatus.textContent = '演出继续，跟住下一拍。';
    performanceFrame = requestAnimationFrame(performanceLoop);
  }
});

const settingsDialog = document.querySelector('#settingsDialog');
const motionToggle = document.querySelector('#motionToggle');
const effectsToggle = document.querySelector('#effectsToggle');
const settingsStatus = document.querySelector('#settingsStatus');
const installAppButton = document.querySelector('#installAppBtn');
const resetSaveButton = document.querySelector('#resetSaveBtn');
let deferredInstallPrompt;
let resetSaveTimer;
let resetSaveArmed = false;

const isStandalone = () => matchMedia('(display-mode: standalone)').matches || navigator.standalone === true;
const setSettingsStatus = message => {
  settingsStatus.textContent = message;
};

const updateSettingsSummary = () => {
  motionToggle.checked = preferences.motionEnabled;
  effectsToggle.checked = preferences.reducedEffects;
  document.querySelector('#settingsBestScore').textContent = formatNumber(state.bestScore);
  document.querySelector('#settingsOwnedCount').textContent = `${MEMBERS.filter(member => isOwned(member.id)).length} / ${MEMBERS.length}`;
  installAppButton.disabled = isStandalone();
  installAppButton.textContent = isStandalone() ? '已安装到设备' : '安装到设备';
};

const disarmResetSave = () => {
  clearTimeout(resetSaveTimer);
  resetSaveArmed = false;
  resetSaveButton.classList.remove('is-armed');
  resetSaveButton.textContent = '重置游戏进度';
};

const openSettingsDialog = () => {
  disarmResetSave();
  updateSettingsSummary();
  setSettingsStatus('设置已就绪。');
  openModal(settingsDialog);
};

const closeSettingsDialog = () => {
  disarmResetSave();
  closeModal(settingsDialog);
};

document.querySelector('#settingsButton').addEventListener('click', event => {
  event.preventDefault();
  event.stopPropagation();
  openSettingsDialog();
});
document.querySelector('#closeSettingsDialog').addEventListener('click', closeSettingsDialog);
settingsDialog.addEventListener('cancel', event => {
  event.preventDefault();
  closeSettingsDialog();
});

const updateVisualPreferences = () => {
  preferences = {
    motionEnabled: motionToggle.checked,
    reducedEffects: effectsToggle.checked
  };
  savePreferences();
  applyPreferences(true);
  setSettingsStatus(preferences.motionEnabled ? '动态效果设置已保存。' : '已切换为省流量静态模式。');
};
motionToggle.addEventListener('change', updateVisualPreferences);
effectsToggle.addEventListener('change', updateVisualPreferences);

document.querySelector('#exportSaveBtn').addEventListener('click', () => {
  const backup = createBackup(state, preferences);
  const blob = new Blob([`${JSON.stringify(backup, null, 2)}\n`], { type: 'application/json' });
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `cho-siren-save-${new Date().toISOString().slice(0, 10)}.json`;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
  setSettingsStatus('存档已导出，可复制到另一台电脑后导入。');
});

document.querySelector('#importSaveInput').addEventListener('change', async event => {
  const [file] = event.target.files;
  if (!file) return;
  try {
    const restored = restoreBackup(await file.text());
    if (restored.error === 'UNSUPPORTED_VERSION') throw new Error('存档来自更高版本，请先更新游戏。');
    if (restored.error) throw new Error('文件不是有效的 CHO-SIREN 存档。');
    state = restored.state;
    preferences = restored.preferences;
    const saved = saveState() && savePreferences();
    if (!saved) throw new Error('浏览器阻止了本地存储，请检查隐私设置。');
    applyPreferences(true);
    renderAll();
    updateSettingsSummary();
    setSettingsStatus('存档导入成功，进度与显示设置已恢复。');
    showToast('存档导入成功');
  } catch (error) {
    setSettingsStatus(error.message || '存档导入失败。');
  } finally {
    event.target.value = '';
  }
});

resetSaveButton.addEventListener('click', () => {
  if (!resetSaveArmed) {
    resetSaveArmed = true;
    resetSaveButton.classList.add('is-armed');
    resetSaveButton.textContent = '再次点击确认重置';
    setSettingsStatus('此操作会清除成员、货币和演出记录；5 秒内再次点击确认。');
    resetSaveTimer = setTimeout(disarmResetSave, 5000);
    return;
  }
  disarmResetSave();
  state = normalizeState();
  featureState = { ...DEFAULT_FEATURE_STATE };
  chooseAuditionCandidates();
  saveState();
  saveFeatureState();
  renderAll();
  updateSettingsSummary();
  setSettingsStatus('游戏进度已重置，显示设置保持不变。');
  showToast('游戏进度已重置');
});

window.addEventListener('beforeinstallprompt', event => {
  event.preventDefault();
  deferredInstallPrompt = event;
  updateSettingsSummary();
});

window.addEventListener('appinstalled', () => {
  deferredInstallPrompt = undefined;
  updateSettingsSummary();
  setSettingsStatus('CHO-SIREN 已安装到设备。');
});

installAppButton.addEventListener('click', async () => {
  if (isStandalone()) {
    setSettingsStatus('当前已经是安装版。');
    return;
  }
  if (!deferredInstallPrompt) {
    setSettingsStatus('如浏览器支持，请在浏览器菜单中选择“安装应用”或“添加到主屏幕”。');
    return;
  }
  deferredInstallPrompt.prompt();
  const choice = await deferredInstallPrompt.userChoice;
  deferredInstallPrompt = undefined;
  setSettingsStatus(choice.outcome === 'accepted' ? '正在安装应用……' : '已取消安装。');
});

renderAll();

if ('serviceWorker' in navigator) {
  window.addEventListener('load', () => {
    navigator.serviceWorker
      .register('./service-worker.js?v=19', { updateViaCache: 'none' })
      .then(registration => registration.update())
      .catch(() => {});
  }, { once: true });
}
