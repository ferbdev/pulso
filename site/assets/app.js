(() => {
  const DOWNLOAD_URL = 'https://github.com/ferbdev/pulso/releases/latest/download/Pulso.exe';
  const PROTOCOL_URL = 'pulso://open';
  const PROTOCOL_TIMEOUT_MS = 1500;

  const messages = {
    'pt-BR': {
      downloadingTitle: 'Download iniciado',
      downloadingSteps: [
        'Abra o <strong>Pulso.exe</strong> na barra de downloads do navegador.',
        'Se aparecer “O Windows protegeu o computador”, clique em <strong>Mais informações → Executar assim mesmo</strong>.',
        'Na próxima visita, o botão “Abrir widget no Windows” abre o Pulso direto.',
      ],
      openedTitle: 'Abrindo o Pulso…',
      openedText: 'Confirme “Abrir Pulso” no aviso do navegador, se ele aparecer.',
      notWindows: 'O Pulso é para Windows 10 e 11. Abra esta página no seu PC para baixar.',
      close: 'Fechar',
    },
    en: {
      downloadingTitle: 'Download started',
      downloadingSteps: [
        'Open <strong>Pulso.exe</strong> from your browser’s downloads.',
        'If you see “Windows protected your PC”, click <strong>More info → Run anyway</strong>.',
        'Next time, the “Open widget on Windows” button launches Pulso directly.',
      ],
      openedTitle: 'Opening Pulso…',
      openedText: 'Confirm “Open Pulso” in your browser’s prompt, if it shows up.',
      notWindows: 'Pulso is for Windows 10 and 11. Open this page on your PC to download it.',
      close: 'Close',
    },
  };

  const lang = document.documentElement.lang === 'en' ? 'en' : 'pt-BR';
  const t = messages[lang];
  const isWindows = /Windows/i.test(navigator.userAgent);

  const toast = document.createElement('div');
  toast.className = 'toast glass';
  toast.setAttribute('role', 'status');
  toast.setAttribute('aria-live', 'polite');
  document.body.appendChild(toast);
  let toastTimer;

  function showToast(title, bodyHtml, timeoutMs) {
    toast.innerHTML =
      `<button type="button" aria-label="${t.close}">×</button><h4>${title}</h4>${bodyHtml}`;
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

  function startDownload() {
    const link = document.createElement('a');
    link.href = DOWNLOAD_URL;
    link.rel = 'noopener';
    document.body.appendChild(link);
    link.click();
    link.remove();
    showToast(t.downloadingTitle, `<ol>${t.downloadingSteps.map((s) => `<li>${s}</li>`).join('')}</ol>`, 20000);
  }

  // Tenta abrir via pulso://. Se o navegador perder o foco (diálogo "Abrir Pulso?" ou o app abriu),
  // o protocolo está registrado; se nada acontecer, o Pulso ainda não foi executado nesta máquina: baixa.
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
