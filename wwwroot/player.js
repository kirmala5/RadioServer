(function () {
    const audio = document.getElementById('audio');
    const stationArtwork = document.getElementById('stationArtwork');
    const stationArtworkImg = document.getElementById('stationArtworkImg');
    const stationLetter = document.getElementById('stationLetter');
    const trackTitle = document.getElementById('trackTitle');
    const stationName = document.getElementById('stationName');
    const btnPlay = document.getElementById('btnPlay');
    const btnPause = document.getElementById('btnPause');
    const progress = document.getElementById('progress');
    const progressFill = document.getElementById('progressFill');
    const progressThumb = document.getElementById('progressThumb');
    const timeDisplay = document.getElementById('timeDisplay');
    const listenersCount = document.getElementById('listenersCount');
    const stationsGrid = document.getElementById('stationsGrid');
    const waveform = document.getElementById('waveform');

    let currentStation = null;
    let isPlaying = false;
    let stations = [];

    // ---------- Волна plaza ----------
    function renderWaveform() {
        waveform.innerHTML = '';
        for (let i = 0; i < 28; i++) {
            const bar = document.createElement('div');
            bar.className = 'waveform-bar';
            bar.style.animationDelay = (i * 0.05) + 's';
            waveform.appendChild(bar);
        }
    }
    renderWaveform();

    function setWaveformActive(active) {
        const bars = waveform.querySelectorAll('.waveform-bar');
        bars.forEach((b) => b.classList.toggle('active', active));
    }

    // ---------- Градиент обложки по названию станции ----------
    function stationGradient(stationId) {
        let hash = 0;
        for (let i = 0; i < stationId.length; i++) {
            hash = stationId.charCodeAt(i) + ((hash << 5) - hash);
        }
        const hue = Math.abs(hash) % 360;
        const hue2 = (hue + 60) % 360;
        return `linear-gradient(135deg, hsl(${hue}, 70%, 55%), hsl(${hue2}, 70%, 55%))`;
    }

    // ---------- Загрузка списка станций ----------
    async function loadStations() {
        try {
            const res = await fetch('/stations', { cache: 'no-store' });
            if (!res.ok) throw new Error('stations endpoint failed');
            stations = await res.json();
            renderStations();
            if (stations.length > 0 && !currentStation) {
                currentStation = stations[0].id;
                tune(currentStation);
            }
            refreshStatus();
        } catch (err) {
            console.error('Не удалось загрузить станции:', err);
            stationsGrid.innerHTML =
                '<div class="empty-state">Не удалось загрузить станции. Убедитесь, что сервер запущен.</div>';
        }
    }

    function renderStations() {
        stationsGrid.innerHTML = '';
        for (const s of stations) {
            const card = document.createElement('article');
            card.className = 'station-card';
            const isActive = s.id === currentStation;
            if (isActive) card.classList.add('is-active');

            const grad = stationGradient(s.id);
            const letter = s.id.charAt(0).toUpperCase();

            card.innerHTML = `
                <div class="station-artwork" role="button" tabindex="0" aria-label="Воспроизвести ${s.name}">
                    <img class="station-artwork-img" src="" alt="">
                    <div class="station-play-overlay" aria-hidden="true"></div>
                    <span class="station-letter" style="position:absolute;inset:0;display:flex;align-items:center;justify-content:center;font-size:36px;font-weight:900;color:#fff;letter-spacing:-1px;">${letter}</span>
                </div>
                <div class="station-body">
                    <div class="station-title" title="${escapeHtml(s.name)}">${escapeHtml(s.name)}</div>
                    <div class="station-desc">HTTP-поток &middot; MP3</div>
                    <div class="station-footer">
                        <div class="station-listeners">
                            <span class="listeners-dot" style="width:6px;height:6px;background:var(--plaza-accent);border-radius:50%;box-shadow:0 0 8px var(--plaza-accent);display:inline-block;"></span>
                            ${s.listeners} слушателей
                        </div>
                        <button class="btn-play-small" data-id="${s.id}">${isActive ? 'Играет' : 'Играть'}</button>
                    </div>
                </div>
            `;

            // Применяем градиент к обложке карточки
            const artworkDiv = card.querySelector('.station-artwork');
            artworkDiv.style.background = grad;

            artworkDiv.addEventListener('click', () => tune(s.id));
            artworkDiv.addEventListener('keydown', (e) => { if (e.key === 'Enter') tune(s.id); });
            card.querySelector('.btn-play-small').addEventListener('click', (e) => { e.stopPropagation(); tune(s.id); });

            stationsGrid.appendChild(card);
        }
    }

    // ---------- Настройка на станцию ----------
    window.tune = function tune(stationId) {
        currentStation = stationId;
        audio.src = `/stream/${stationId}?t=${Date.now()}`;
        audio.load();
        if (isPlaying) {
            audio.play().catch(() => {});
        }

        const s = stations.find((x) => x.id === stationId);
        if (s) {
            stationLetter.textContent = stationId.charAt(0).toUpperCase();
            stationArtwork.style.background = stationGradient(stationId);
            stationArtworkImg.src = '';
            stationName.textContent = s.name;
            trackTitle.textContent = s.currentTrack || '—';
            listenersCount.textContent = s.listeners;
        }

        renderStations();
    };

    // ---------- Управление воспроизведением ----------
    btnPlay.addEventListener('click', () => {
        audio.play().then(() => {
            isPlaying = true;
            btnPlay.disabled = true;
            btnPause.disabled = false;
            setWaveformActive(true);
        }).catch((err) => {
            console.error('Play failed:', err);
        });
    });

    btnPause.addEventListener('click', () => {
        audio.pause();
        isPlaying = false;
        btnPlay.disabled = false;
        btnPause.disabled = true;
        setWaveformActive(false);
    });

    audio.addEventListener('play', () => {
        isPlaying = true;
        btnPlay.disabled = true;
        btnPause.disabled = false;
        setWaveformActive(true);
    });
    audio.addEventListener('pause', () => {
        isPlaying = false;
        btnPlay.disabled = false;
        btnPause.disabled = true;
        setWaveformActive(false);
    });
    audio.addEventListener('ended', () => {
        isPlaying = false;
        btnPlay.disabled = false;
        btnPause.disabled = true;
        setWaveformActive(false);
    });

    // ---------- Прогресс-бар ----------
    setInterval(() => {
        if (!isNaN(audio.currentTime)) {
            const m = Math.floor(audio.currentTime / 60);
            const s = Math.floor(audio.currentTime % 60).toString().padStart(2, '0');
            timeDisplay.textContent = `${m}:${s}`;
            const pct = audio.duration > 0 ? (audio.currentTime / audio.duration) * 100 : 0;
            progressFill.style.width = `${Math.min(pct, 100)}%`;
            progressThumb.style.left = `${Math.min(pct, 100)}%`;
        }
    }, 500);

    progress.addEventListener('click', (e) => {
        const rect = progress.getBoundingClientRect();
        const pct = (e.clientX - rect.left) / rect.width;
        if (audio.duration) {
            audio.currentTime = pct * audio.duration;
        }
    });

    // ---------- Статус станций (каждые 5 с) ----------
    async function refreshStatus() {
        try {
            const res = await fetch('/stations', { cache: 'no-store' });
            const data = await res.json();
            stations = data;

            const cur = data.find((x) => x.id === currentStation);
            if (cur) {
                trackTitle.textContent = cur.currentTrack || '—';
                listenersCount.textContent = cur.listeners;
            }
            renderStations();
        } catch (err) {
            console.error('Не удалось обновить статус:', err);
        }
    }

    setInterval(refreshStatus, 5000);

    // ---------- Старт ----------
    loadStations();

    // ---------- Вспомогательно ----------
    function escapeHtml(text) {
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    }
})();
