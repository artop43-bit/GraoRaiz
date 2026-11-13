// Comemoração da compra: grãos de café e confetes caindo uma única vez.
function comemorarCompra() {
  const reduzir = window.matchMedia('(prefers-reduced-motion: reduce)').matches;

  if (reduzir) return;

  const canvas = document.createElement('canvas');
  canvas.id = 'confete';
  canvas.setAttribute('aria-hidden', 'true');
  document.body.appendChild(canvas);
  const ctx = canvas.getContext('2d');
  const ajustar = () => { canvas.width = innerWidth; canvas.height = innerHeight; };
  ajustar();
  addEventListener('resize', ajustar);

  const cores = ['#1f5c4a', '#b3203a', '#e7d6bb', '#3b2418', '#d9a441'];
  const pecas = Array.from({ length: 140 }, () => ({
    x: Math.random() * canvas.width,
    y: -20 - Math.random() * canvas.height * 0.6,
    vx: (Math.random() - 0.5) * 2,
    vy: 2 + Math.random() * 3,
    giro: Math.random() * Math.PI,
    vg: (Math.random() - 0.5) * 0.2,
    tam: 6 + Math.random() * 6,
    cor: cores[Math.floor(Math.random() * cores.length)],
    grao: Math.random() < 0.35
  }));

  const inicio = performance.now();
  (function quadro(agora) {
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    for (const p of pecas) {
      p.x += p.vx; p.y += p.vy; p.giro += p.vg;
      ctx.save();
      ctx.translate(p.x, p.y);
      ctx.rotate(p.giro);
      if (p.grao) {
        // grão de café: elipse marrom com o sulco central
        ctx.fillStyle = '#5a3a28';
        ctx.beginPath(); ctx.ellipse(0, 0, p.tam * 0.6, p.tam * 0.9, 0, 0, Math.PI * 2); ctx.fill();
        ctx.strokeStyle = '#2b1910'; ctx.lineWidth = 1.2;
        ctx.beginPath(); ctx.moveTo(0, -p.tam * 0.7); ctx.quadraticCurveTo(p.tam * 0.25, 0, 0, p.tam * 0.7); ctx.stroke();
      } else {
        ctx.fillStyle = p.cor;
        ctx.fillRect(-p.tam / 2, -p.tam / 4, p.tam, p.tam / 2);
      }
      ctx.restore();
    }
    if (agora - inicio < 4500) requestAnimationFrame(quadro);
    else canvas.remove();
  })(inicio);
}

// Painel do administrador: consulta o estoque no banco a cada 3 segundos e atualiza a tabela.
function monitorarEstoque(url) {
  const classes = { Normal: 'ok', Repor: 'baixo', Esgotado: 'esgotado', Inativo: 'inativo' };

  async function atualizar() {
    try {
      const resp = await fetch(url, { headers: { 'Accept': 'application/json' }, cache: 'no-store' });
      if (!resp.ok) throw new Error(resp.status);
      const dados = await resp.json();

      for (const p of dados.produtos) {
        const linha = document.querySelector(`[data-estoque-id="${p.id}"]`);
        if (!linha) continue;
        const qtd = linha.querySelector('[data-qtd]');
        if (qtd && qtd.textContent !== String(p.quantidade)) {
          qtd.textContent = p.quantidade;
          linha.classList.remove('mudou');
          void linha.offsetWidth; // reinicia a animação de destaque
          linha.classList.add('mudou');
        }
        const min = linha.querySelector('[data-min]');
        if (min) min.textContent = p.ehKit ? '—' : p.estoqueMinimo;
        const sit = linha.querySelector('[data-situacao]');
        if (sit && sit.dataset.atual !== p.situacao) {
          sit.dataset.atual = p.situacao;
          sit.innerHTML = `<span class="selo ${classes[p.situacao]}">${p.situacao}</span>`;
        }
      }
      document.querySelectorAll('[data-alertas]').forEach(el => { el.textContent = dados.alertas; });

      // Lista "Hora de torrar" também atualiza sem recarregar a página.
      const lista = document.querySelector('[data-lista-alertas]');
      if (lista) {
        lista.replaceChildren(...dados.listaAlertas.map(a => {
          const li = document.createElement('li');
          const link = document.createElement('a');
          link.href = a.url;
          const nome = document.createElement('strong');
          nome.textContent = a.nomeProduto;
          link.append(nome);
          const info = document.createElement('span');
          info.className = 'muted';
          info.textContent = `${a.quantidadeAtual} em estoque · ${a.data}`;
          li.append(link, document.createElement('br'), info);
          return li;
        }));
        const vazio = document.querySelector('[data-sem-alertas]');
        if (vazio) vazio.hidden = dados.listaAlertas.length > 0;
      }
      document.querySelectorAll('[data-ao-vivo]').forEach(el => el.classList.remove('offline'));
      document.querySelectorAll('[data-hora]').forEach(el => { el.textContent = dados.atualizadoEm; });
    } catch {
      document.querySelectorAll('[data-ao-vivo]').forEach(el => el.classList.add('offline'));
    }
  }

  atualizar();
  setInterval(atualizar, 3000);
}

// ---------- Pequenas facilidades usadas em várias telas ----------
document.addEventListener('DOMContentLoaded', () => {
  // Envia o formulário assim que o campo muda (ordenação, quantidade, filtros).
  document.querySelectorAll('[data-enviar-ao-mudar]').forEach(campo => {
    campo.addEventListener('change', () => campo.form?.requestSubmit());
  });

  // Pede confirmação antes de ações que não têm volta (cancelar pedido, cancelar assinatura).
  document.querySelectorAll('form[data-confirmar]').forEach(form => {
    form.addEventListener('submit', e => { if (!confirm(form.dataset.confirmar)) e.preventDefault(); });
  });

  // Sino de notificações: consulta a quantidade de não lidas a cada 10 segundos.
  const sino = document.querySelector('[data-sino]');
  if (sino) {
    const contador = sino.querySelector('[data-contador]');
    const atualizarSino = async () => {
      try {
        const r = await fetch(sino.dataset.sino, { headers: { Accept: 'application/json' }, cache: 'no-store' });
        if (!r.ok) return;
        const { naoLidas } = await r.json();
        if (String(naoLidas) !== contador.textContent && naoLidas > 0) sino.classList.add('tocou');
        contador.textContent = naoLidas;
        contador.hidden = naoLidas === 0;
        sino.setAttribute('aria-label', naoLidas ? `Notificações: ${naoLidas} nova(s)` : 'Notificações');
      } catch { /* sem conexão: tenta de novo no próximo ciclo */ }
    };
    setInterval(atualizarSino, 10000);
  }

  // CEP: busca rua, bairro, cidade e UF no ViaCEP (serviço público e gratuito).
  document.querySelectorAll('[data-form-endereco]').forEach(form => {
    const cep = form.querySelector('[data-cep]');
    const status = form.querySelector('[data-cep-status]');
    cep.addEventListener('input', async () => {
      let numeros = cep.value.replace(/\D/g, '').slice(0, 8);
      cep.value = numeros.length > 5 ? `${numeros.slice(0, 5)}-${numeros.slice(5)}` : numeros;
      if (numeros.length !== 8) return;
      status.textContent = 'Buscando endereço...';
      try {
        const r = await fetch(`https://viacep.com.br/ws/${numeros}/json/`);
        const dados = await r.json();
        if (dados.erro) { status.textContent = 'CEP não encontrado. Preencha o endereço manualmente.'; return; }
        form.querySelectorAll('[data-cep-campo]').forEach(c => { c.value = dados[c.dataset.cepCampo] || c.value; });
        status.textContent = 'Endereço encontrado. Confira e informe o número.';
        form.querySelector('[name="Numero"]')?.focus();
      } catch {
        status.textContent = 'Sem internet para buscar o CEP. Preencha manualmente.';
      }
    });
  });

  // Checkout: ao trocar o endereço, atualiza frete, prazo e total sem recarregar.
  const formFinalizar = document.getElementById('form-finalizar');
  if (formFinalizar) {
    formFinalizar.querySelectorAll('input[name="idEndereco"]').forEach(radio => {
      radio.addEventListener('change', () => {
        for (const campo of ['frete', 'prazo', 'total']) {
          const alvo = document.querySelector(`[data-campo="${campo}"]`);
          if (alvo && radio.dataset[campo]) alvo.textContent = radio.dataset[campo];
        }
        const enderecoAtual = document.querySelector('[data-endereco-atual]');
        if (enderecoAtual) enderecoAtual.value = radio.value;
      });
    });
  }
});
