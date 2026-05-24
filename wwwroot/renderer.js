// renderer.js – UI logic for Library of Ai-Lexandria

const logArea = document.getElementById('logArea');
const refreshLogBtn = document.getElementById('refreshLogBtn');

// Tabs
const tabBtns = document.querySelectorAll('.tab-btn');
const tabContents = document.querySelectorAll('.tab-content');
tabBtns.forEach(btn => {
    btn.addEventListener('click', () => {
        tabBtns.forEach(b => b.classList.remove('active'));
        tabContents.forEach(c => c.classList.remove('active'));
        tabContents.forEach(c => c.style.display = 'none');
        
        btn.classList.add('active');
        const target = document.getElementById('tab-' + btn.dataset.tab);
        if (target) {
            target.classList.add('active');
            target.style.display = 'block';
        }
    });
});

// Bots/Characters UI elements
const addBotBtn = document.getElementById('addBotBtn');
const botsBody = document.getElementById('botsBody');

// Settings UI elements
const settingMasterToken = document.getElementById('settingMasterToken');
const settingStatusChannel = document.getElementById('settingStatusChannel');
const settingNovelAiKey = document.getElementById('settingNovelAiKey');
const settingGithubRepo = document.getElementById('settingGithubRepo');
const settingUpdateMode = document.getElementById('settingUpdateMode');
const saveSettingsBtn = document.getElementById('saveSettingsBtn');
const checkUpdatesBtn = document.getElementById('checkUpdatesBtn');

// Modal UI elements
const botModal = document.getElementById('botModal');
const modalTitle = document.getElementById('modalTitle');
const botName = document.getElementById('botName');
const botChannel = document.getElementById('botChannel');
const botAvatar = document.getElementById('botAvatar');
const botNovelAi = document.getElementById('botNovelAi');
const botPersona = document.getElementById('botPersona');
const advancedToggle = document.getElementById('advancedToggle');
const advancedSettings = document.getElementById('advancedSettings');
const botModel = document.getElementById('botModel');
const botTemp = document.getElementById('botTemp');
const botMemory = document.getElementById('botMemory');
const botSystemPrompt = document.getElementById('botSystemPrompt');
const botAutoStart = document.getElementById('botAutoStart');
const botMentionMode = document.getElementById('botMentionMode');

const cancelBotBtn = document.getElementById('cancelBotBtn');
const saveBotBtn = document.getElementById('saveBotBtn');

let editingBotIndex = -1;
let appSettings = {};

// Helper to post messages to host
function post(action, payload) {
    window.chrome.webview.postMessage(Object.assign({action}, payload || {}));
}

// ---------- Settings ----------
saveSettingsBtn.addEventListener('click', () => {
    appSettings.masterDiscordToken = settingMasterToken.value.trim();
    appSettings.statusChannelId = settingStatusChannel.value.trim();
    appSettings.githubRepo = settingGithubRepo.value.trim();
    appSettings.updateMode = settingUpdateMode.value;
    appSettings.novelAiKey = settingNovelAiKey.value.trim();
    post('saveAppSettings', { settings: appSettings });
});

checkUpdatesBtn.addEventListener('click', () => {
    post('checkUpdates');
});

// ---------- Log ----------
if (refreshLogBtn) refreshLogBtn.addEventListener('click', () => post('requestLogTail'));

// ---------- Characters ----------
function renderBots(bots) {
    botsBody.innerHTML = '';// clear
    bots.forEach((bot, idx) => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td style="padding:0.5rem; text-align:center;">${bot.connected ? '✅' : '❌'}</td>
            <td style="padding:0.5rem;">${bot.name || ''}</td>
            <td style="padding:0.5rem;">${bot.channelId || 'Global'}</td>
            <td style="padding:0.5rem;">
                <button class="btn" data-idx="${idx}" data-action="toggle">${bot.connected ? 'Stop' : 'Start'}</button>
                <button class="btn" data-idx="${idx}" data-action="edit">Edit</button>
                <button class="btn" data-idx="${idx}" data-action="delete">Delete</button>
            </td>`;
        botsBody.appendChild(tr);
    });
}

// Modal logic
advancedToggle.addEventListener('change', () => {
    advancedSettings.style.display = advancedToggle.checked ? 'block' : 'none';
});

function showModal(bot = null, idx = -1) {
    editingBotIndex = idx;
    modalTitle.textContent = bot ? 'Edit Character Plugin' : 'Add Character Plugin';
    botName.value = bot?.name || '';
    botChannel.value = bot?.channelId || '';
    botAvatar.value = bot?.avatarUrl || '';
    botNovelAi.value = bot?.novelAiKey || '';
    botPersona.value = bot?.systemPrompt || '';
    
    // advanced settings
    botModel.value = bot?.novelAiModel || 'kayra-v1';
    botTemp.value = bot?.novelAiTemp || '1.0';
    botMemory.value = bot?.memoryLimit || '20';
    botSystemPrompt.value = bot?.systemPrompt || '';
    botAutoStart.checked = !!bot?.autoStart;
    botMentionMode.checked = !!bot?.mentionMode;
    
    advancedToggle.checked = !!bot?.advanced;
    advancedSettings.style.display = advancedToggle.checked ? 'block' : 'none';
    
    botModal.showModal();
}

addBotBtn.addEventListener('click', () => {
    const current = JSON.parse(botsBody.dataset.bots || '[]');
    if (current.length >= 20) {
        alert('Maximum of 20 characters reached');
        return;
    }
    showModal();
});

cancelBotBtn.addEventListener('click', () => {
    botModal.close();
});

const importCardBtn = document.getElementById('importCardBtn');
if (importCardBtn) {
    importCardBtn.addEventListener('click', () => {
        post('importCard');
    });
}

saveBotBtn.addEventListener('click', () => {
    const name = botName.value.trim();
    if (!name) { alert('Name is required'); return; }
    
    const bot = {
        name,
        channelId: botChannel.value.trim(),
        avatarUrl: botAvatar.value.trim(),
        novelAiKey: botNovelAi.value.trim(),
        systemPrompt: botSystemPrompt.value.trim(),
        advanced: advancedToggle.checked,
        novelAiModel: botModel.value,
        novelAiTemp: parseFloat(botTemp.value) || 1.0,
        memoryLimit: parseInt(botMemory.value, 10) || 20,
        autoStart: botAutoStart.checked,
        mentionMode: botMentionMode.checked,
        connected: false
    };

    const current = JSON.parse(botsBody.dataset.bots || '[]');
    if (editingBotIndex >= 0) {
        bot.connected = current[editingBotIndex].connected;
        current[editingBotIndex] = bot;
    } else {
        current.push(bot);
    }
    
    post('saveBots', {bots: current});
    botModal.close();
});

// Delegate actions from table buttons
botsBody.addEventListener('click', e => {
    const btn = e.target.closest('button');
    if (!btn) return;
    const idx = Number(btn.dataset.idx);
    const action = btn.dataset.action;
    const current = JSON.parse(botsBody.dataset.bots || '[]');
    const bot = current[idx];
    if (!bot) return;
    
    if (action === 'toggle') {
        if (bot.connected) {
            post('stopBot', { botIndex: idx });
        } else {
            post('startBot', { botIndex: idx });
        }
    } else if (action === 'edit') {
        showModal(bot, idx);
    } else if (action === 'delete') {
        if (!confirm('Delete this character?')) return;
        current.splice(idx, 1);
        post('saveBots', {bots: current});
    }
});

// ---------- Host messages ----------
window.chrome.webview.addEventListener('message', ev => {
    const data = ev.data;
    switch (data.action) {
        case 'settingsData':
            appSettings = data.settings || {};
            settingMasterToken.value = appSettings.masterDiscordToken || '';
            settingStatusChannel.value = appSettings.statusChannelId || '';
            settingNovelAiKey.value = appSettings.novelAiKey || '';
            settingGithubRepo.value = appSettings.githubRepo || '';
            settingUpdateMode.value = appSettings.updateMode || 'prompt';
            break;
        case 'saveResult':
            if (!data.success) {
                alert(data.error || 'Save failed');
            }
            break;
        case 'cardImported':
            const newBot = data.bot;
            showModal(newBot, -1);
            alert(`Imported ${newBot.name}! Please review their settings and save.`);
            break;
        case 'botsData':
            botsBody.dataset.bots = JSON.stringify(data.bots);
            renderBots(data.bots);
            break;
        case 'statusUpdate':
            const current = JSON.parse(botsBody.dataset.bots || '[]');
            data.statuses.forEach((status, idx) => {
                if (current[idx]) current[idx].connected = status;
            });
            botsBody.dataset.bots = JSON.stringify(current);
            renderBots(current);
            break;
        case 'logUpdate':
            logArea.textContent += data.line + '\n';
            logArea.scrollTop = logArea.scrollHeight;
            break;
        case 'logFull':
            logArea.textContent = data.content;
            logArea.scrollTop = logArea.scrollHeight;
            break;
    }
});

// Initial load
post('readBots');
post('readAppSettings');
post('requestLogTail');

// Periodic status update
setInterval(() => {
    post('requestStatuses');
}, 1000);
