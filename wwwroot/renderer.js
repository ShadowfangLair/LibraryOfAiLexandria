// renderer.js – UI logic for Library of Ai‑Lexandria

const configArea = document.getElementById('configArea');
const loadBtn = document.getElementById('loadBtn');
const saveBtn = document.getElementById('saveBtn');
const logArea = document.getElementById('logArea');
const refreshLogBtn = document.getElementById('refreshLogBtn');

// Bots UI elements
const addBotBtn = document.getElementById('addBotBtn');
const botsBody = document.getElementById('botsBody');

// Settings UI elements
const settingGithubRepo = document.getElementById('settingGithubRepo');
const settingUpdateMode = document.getElementById('settingUpdateMode');
const saveSettingsBtn = document.getElementById('saveSettingsBtn');
const checkUpdatesBtn = document.getElementById('checkUpdatesBtn');

// Modal UI elements
const botModal = document.getElementById('botModal');
const modalTitle = document.getElementById('modalTitle');
const botName = document.getElementById('botName');
const botDiscord = document.getElementById('botDiscord');
const botNovelAi = document.getElementById('botNovelAi');
const advancedToggle = document.getElementById('advancedToggle');
const advancedSettings = document.getElementById('advancedSettings');
const botModel = document.getElementById('botModel');
const botTemp = document.getElementById('botTemp');
const tempVal = document.getElementById('tempVal');
const botMem = document.getElementById('botMem');
const memVal = document.getElementById('memVal');
const modalCancel = document.getElementById('modalCancel');
const modalSave = document.getElementById('modalSave');

let editingBotIndex = -1;

// Helper to post messages to host
function post(action, payload) {
    window.chrome.webview.postMessage(Object.assign({action}, payload || {}));
}

// ---------- Config ----------
loadBtn.addEventListener('click', () => post('readConfig'));
saveBtn.addEventListener('click', () => {
    try {
        const obj = JSON.parse(configArea.value);
        post('saveConfig', {content: obj});
    } catch (e) {
        alert('Invalid JSON in config area');
    }
});

// ---------- Settings ----------
saveSettingsBtn.addEventListener('click', () => {
    const settings = {
        githubRepo: settingGithubRepo.value.trim(),
        updateMode: settingUpdateMode.value
    };
    post('saveAppSettings', { settings });
});

checkUpdatesBtn.addEventListener('click', () => {
    post('checkUpdates');
});

// ---------- Log ----------
if (refreshLogBtn) refreshLogBtn.addEventListener('click', () => post('requestLogTail'));

// ---------- Bots ----------
function renderBots(bots) {
    botsBody.innerHTML = '';// clear
    bots.forEach((bot, idx) => {
        const tr = document.createElement('tr');
        tr.innerHTML = `
            <td style="padding:0.5rem;">${bot.name || ''}</td>
            <td style="padding:0.5rem;">${bot.discordToken ? '••••••••' : ''}</td>
            <td style="padding:0.5rem;">${bot.novelAiKey ? '••••••••' : ''}</td>
            <td style="padding:0.5rem; text-align:center;">${bot.connected ? '✅' : '❌'}</td>
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
botTemp.addEventListener('input', () => tempVal.textContent = parseFloat(botTemp.value).toFixed(1));
botMem.addEventListener('input', () => memVal.textContent = botMem.value);

function showModal(bot = null, idx = -1) {
    editingBotIndex = idx;
    modalTitle.textContent = bot ? 'Edit Bot' : 'Add Bot';
    botName.value = bot?.name || '';
    botDiscord.value = bot?.discordToken || '';
    botNovelAi.value = bot?.novelAiKey || '';
    
    // advanced settings
    botModel.value = bot?.novelAiModel || 'kayra-v1';
    botTemp.value = bot?.novelAiTemp || '1.0';
    tempVal.textContent = parseFloat(botTemp.value).toFixed(1);
    botMem.value = bot?.memoryLimit || '20';
    memVal.textContent = botMem.value;
    
    advancedToggle.checked = !!bot?.advanced;
    advancedSettings.style.display = advancedToggle.checked ? 'block' : 'none';
    
    botModal.showModal();
}

addBotBtn.addEventListener('click', () => {
    const current = JSON.parse(botsBody.dataset.bots || '[]');
    if (current.length >= 10) {
        alert('Maximum of 10 bots reached');
        return;
    }
    showModal();
});

modalCancel.addEventListener('click', () => {
    botModal.close();
});

modalSave.addEventListener('click', () => {
    const name = botName.value.trim();
    if (!name) { alert('Name is required'); return; }
    
    const bot = {
        name,
        discordToken: botDiscord.value.trim(),
        novelAiKey: botNovelAi.value.trim(),
        advanced: advancedToggle.checked,
        novelAiModel: botModel.value,
        novelAiTemp: parseFloat(botTemp.value),
        memoryLimit: parseInt(botMem.value, 10),
        connected: false // assume disconnected when saving config
    };

    const current = JSON.parse(botsBody.dataset.bots || '[]');
    if (editingBotIndex >= 0) {
        // preserve connected state if editing
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
        // Here we send a request to the host to start or stop the bot runtime
        if (bot.connected) {
            post('stopBot', { botIndex: idx });
        } else {
            post('startBot', { botIndex: idx });
        }
    } else if (action === 'edit') {
        showModal(bot, idx);
    } else if (action === 'delete') {
        if (!confirm('Delete this bot?')) return;
        current.splice(idx, 1);
        post('saveBots', {bots: current});
    }
});

// ---------- Host messages ----------
window.chrome.webview.addEventListener('message', ev => {
    const data = ev.data;
    switch (data.action) {
        case 'configData':
            configArea.value = JSON.stringify(data.content, null, 2);
            break;
        case 'settingsData':
            settingGithubRepo.value = data.settings?.githubRepo || '';
            settingUpdateMode.value = data.settings?.updateMode || 'prompt';
            break;
        case 'saveResult':
            if (data.success) {
                console.log('Save successful');
            } else {
                alert(data.error || 'Save failed');
            }
            break;
        case 'botsData':
            botsBody.dataset.bots = JSON.stringify(data.bots);
            renderBots(data.bots);
            break;
        case 'logUpdate':
            logArea.textContent += data.line + '\n';
            logArea.scrollTop = logArea.scrollHeight;
            break;
        case 'logFull':
            logArea.textContent = data.content;
            logArea.scrollTop = logArea.scrollHeight;
            break;
        case 'toast':
            alert(data.title + ': ' + data.message);
            break;
    }
});

// Initial load
post('readConfig');
post('readBots');
post('readAppSettings');
post('requestLogTail');
