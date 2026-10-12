(() => {
  const DOWNLOAD_URL = 'https://github.com/ferbdev/pulso/releases/latest/download/Pulso.exe';
  const PROTOCOL_URL = 'pulso://open';
  const PROTOCOL_TIMEOUT_MS = 1500;
  const DOWNLOADED_KEY = 'pulso.downloaded';
  // Quando o Pulso.exe estiver assinado (SignPath), o passo do SmartScreen deixa de ser necessário.
  const SIGNED = false;

  const messages = {
    'pt-BR': {
      guideTitle: 'Falta só abrir o arquivo',
      notOpenedYet: 'Parece que o Pulso ainda não foi aberto neste PC. Abra o arquivo baixado ou baixe de novo.',
      browserStep: {
        edge: 'Na janela de downloads do Edge, aqui em cima, clique em <strong>Abrir arquivo</strong>.',
        chrome: 'Clique no ícone de downloads do Chrome, aqui em cima, e depois em <strong>Pulso.exe</strong>.',
        firefox: 'Clique na seta de downloads do Firefox, aqui em cima, e depois em <strong>Pulso.exe</strong>.',
        other: 'Abra o <strong>Pulso.exe</strong> na lista de downloads do navegador.',
      },
      shortcut: 'Não achou? <kbd>Ctrl</kbd> + <kbd>J</kbd> abre os downloads.',
      smartscreenStep: 'Se o Windows mostrar o aviso azul, clique em <strong>Mais informações</strong> e depois em <strong>Executar assim mesmo</strong>.',
      smartscreenTitle: 'O Windows protegeu o computador',
      smartscreenMore: 'Mais informações',
      smartscreenRun: 'Executar assim mesmo',
      adminStep: 'Confirme o pedido de administrador. O widget aparece no canto da tela.',
      nextTime: 'Da próxima vez, o botão “Abrir widget no Windows” abre o Pulso direto.',
      done: 'Já abri, obrigado',
      downloadAgain: 'Baixar de novo',
      openedTitle: 'Abrindo o Pulso…',
      openedText: 'Confirme “Abrir Pulso” no aviso do navegador, se ele aparecer.',
      notWindows: 'O Pulso é para Windows 10 e 11. Abra esta página no seu PC para baixar.',
      close: 'Fechar',
    },
    en: {
      guideTitle: 'Just open the file',
      notOpenedYet: 'It looks like Pulso hasn’t been opened on this PC yet. Open the downloaded file or download it again.',
      browserStep: {
        edge: 'In Edge’s downloads panel up here, click <strong>Open file</strong>.',
        chrome: 'Click Chrome’s downloads icon up here, then <strong>Pulso.exe</strong>.',
        firefox: 'Click Firefox’s downloads arrow up here, then <strong>Pulso.exe</strong>.',
        other: 'Open <strong>Pulso.exe</strong> from your browser’s downloads list.',
      },
      shortcut: 'Can’t find it? <kbd>Ctrl</kbd> + <kbd>J</kbd> opens your downloads.',
      smartscreenStep: 'If Windows shows the blue warning, click <strong>More info</strong>, then <strong>Run anyway</strong>.',
      smartscreenTitle: 'Windows protected your PC',
      smartscreenMore: 'More info',
      smartscreenRun: 'Run anyway',
      adminStep: 'Confirm the administrator prompt. The widget appears in the corner of your screen.',
      nextTime: 'Next time, the “Open widget on Windows” button launches Pulso directly.',
      done: 'Done, thanks',
      downloadAgain: 'Download again',
      openedTitle: 'Opening Pulso…',
      openedText: 'Confirm “Open Pulso” in your browser’s prompt, if it shows up.',
      notWindows: 'Pulso is for Windows 10 and 11. Open this page on your PC to download it.',
      close: 'Close',
    },
  };

  const lang = document.documentElement.lang === 'en' ? 'en' : 'pt-BR';
  const t = messages[lang];
  const isWindows = /Windows/i.test(navigator.userAgent);
  const browser = detectBrowser(navigator.userAgent);

  function detectBrowser(ua) {
    if (/Edg\//.test(ua)) return 'edge';
    if (/Firefox\//.test(ua)) return 'firefox';
    if (/Chrome\//.test(ua)) return 'chrome';
    return 'other';
  }

  // localStorage pode falhar (janela anônima, cookies bloqueados): é só uma conveniência.
  function wasDownloaded() {
    try { return localStorage.getItem(DOWNLOADED_KEY) === '1'; } catch { return false; }
  }

  function markDownloaded() {
    try { localStorage.setItem(DOWNLOADED_KEY, '1'); } catch { /* ignora */ }
  }

  // Aviso curto (ex.: "Abrindo o Pulso…").
  const toast = document.createElement('div');
  toast.className = 'toast glass';
  toast.setAttribute('role', 'status');
  toast.setAttribute('aria-live', 'polite');
  document.body.appendChild(toast);
  let toastTimer;

  function showToast(title, bodyHtml, timeoutMs) {
    toast.innerHTML = `<button type="button" aria-label="${t.close}">×</button><h4>${title}</h4>${bodyHtml}`;
    toast.querySelector('button').addEventListener('click', hideToast);
    toast.classList.add('show');
    clearTimeout(toastTimer);
    if (timeoutMs) {
      toastTimer = setTimeout(hideToast, timeoutMs);
    }
  }

  function hideToast() {
    toast.classList.remove('show');
  }

  // Guia pós-download: seta para a área de downloads do navegador + passos.
  const guide = document.createElement('div');
  guide.className = 'download-guide';
  guide.setAttribute('role', 'dialog');
  guide.setAttribute('aria-modal', 'false');
  guide.setAttribute('aria-labelledby', 'download-guide-title');
  document.body.appendChild(guide);

  function showGuide({ notOpenedYet = false } = {}) {
    hideToast();
    const smartscreen = SIGNED ? '' : `
      <li>
        ${t.smartscreenStep}
        <div class="smartscreen-mock" aria-hidden="true">
          <span class="ss-title">${t.smartscreenTitle}</span>
          <span class="ss-more">${t.smartscreenMore}</span>
          <span class="ss-run">${t.smartscreenRun}</span>
        </div>
      </li>`;

    guide.innerHTML = `
      <div class="guide-arrow" aria-hidden="true">
        <svg viewBox="0 0 48 48" fill="none" stroke="currentColor" stroke-width="4" stroke-linecap="round" stroke-linejoin="round"><path d="M12 36 36 12M18 12h18v18"/></svg>
      </div>
      <div class="guide-panel glass">
        <button type="button" class="guide-close" aria-label="${t.close}">×</button>
        <h4 id="download-guide-title">${t.guideTitle}</h4>
        ${notOpenedYet ? `<p class="guide-notice">${t.notOpenedYet}</p>` : ''}
        <ol class="guide-steps">
          <li>${t.browserStep[browser]}<span class="guide-hint">${t.shortcut}</span></li>
          ${smartscreen}
          <li>${t.adminStep}</li>
        </ol>
        <p class="guide-next">${t.nextTime}</p>
        <div class="guide-actions">
          <button type="button" class="btn btn-primary" data-guide="done">${t.done}</button>
          <a class="btn btn-ghost" href="${DOWNLOAD_URL}" data-guide="again">${t.downloadAgain}</a>
        </div>
      </div>`;

    guide.querySelector('.guide-close').addEventListener('click', hideGuide);
    guide.querySelector('[data-guide="done"]').addEventListener('click', hideGuide);
    guide.querySelector('[data-guide="again"]').addEventListener('click', (event) => {
      event.preventDefault();
      startDownload();
    });
    guide.classList.add('show');
    guide.querySelector('[data-guide="done"]').focus({ preventScroll: true });
  }

  function hideGuide() {
    guide.classList.remove('show');
  }

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') {
      hideGuide();
      hideToast();
    }
  });

  function startDownload() {
    const link = document.createElement('a');
    link.href = DOWNLOAD_URL;
    link.rel = 'noopener';
    document.body.appendChild(link);
    link.click();
    link.remove();
    markDownloaded();
    showGuide();
  }

  // Tenta abrir via pulso://. Se o navegador perder o foco (diálogo "Abrir Pulso?" ou o app abriu),
  // o protocolo está registrado. Se nada acontecer, o Pulso ainda não foi executado nesta máquina:
  // na primeira visita baixa; se já baixou antes, mostra o guia em vez de baixar de novo sem avisar.
  function openWidget() {
    let handled = false;
    const markHandled = () => { handled = true; };
    window.addEventListener('blur', markHandled, { once: true });
    document.addEventListener('visibilitychange', markHandled, { once: true });

    window.location.href = PROTOCOL_URL;

    setTimeout(() => {
      window.removeEventListener('blur', markHandled);
      document.removeEventListener('visibilitychange', markHandled);
      if (handled) {
        showToast(t.openedTitle, `<p>${t.openedText}</p>`, 6000);
      } else if (wasDownloaded()) {
        showGuide({ notOpenedYet: true });
      } else {
        startDownload();
      }
    }, PROTOCOL_TIMEOUT_MS);
  }

  document.querySelectorAll('[data-action="open"]').forEach((button) => {
    if (!isWindows) {
      button.disabled = true;
      button.title = t.notWindows;
      return;
    }
    button.addEventListener('click', openWidget);
  });

  document.querySelectorAll('[data-action="download"]').forEach((link) => {
    link.href = DOWNLOAD_URL;
    link.addEventListener('click', (event) => {
      event.preventDefault();
      startDownload();
    });
  });

  if (!isWindows) {
    document.querySelectorAll('[data-platform-note]').forEach((el) => {
      el.textContent = t.notWindows;
    });
  }
})();
