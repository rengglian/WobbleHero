// Wobble Sumo Cockpit
    const asBtnInvertPitch = document.getElementById('asBtnInvertPitch');

    let currentWaveSpeed = 28;
    let currentCosmicDensity = 'medium';

    // Wobble Sumo Elements
    const viewWobbleSumo = document.getElementById('view-WobbleSumo');
    const sumoStateBadge = document.getElementById('sumoStateBadge');
    const sumoScore = document.getElementById('sumoScore');
    const sumoKnockouts = document.getElementById('sumoKnockouts');
    const sumoOpponents = document.getElementById('sumoOpponents');
    const sumoWave = document.getElementById('sumoWave');
    const sumoRingIntegrity = document.getElementById('sumoRingIntegrity');
    const sumoShockwaveBadge = document.getElementById('sumoShockwaveBadge');
    const sumoJerkLabel = document.getElementById('sumoJerkLabel');
    const sumoRateLabel = document.getElementById('sumoRateLabel');
    const btnSumoShockwave = document.getElementById('btnSumoShockwave');
    const sumoDiffNovice = document.getElementById('sumoDiffNovice');
    const sumoDiffYokozuna = document.getElementById('sumoDiffYokozuna');
    const sumoSizeCompact = document.getElementById('sumoSizeCompact');

    function renderSumoTelemetry(data, b, normX, normY, rollDeg, pitchDeg) {
      if (sumoStateBadge) sumoStateBadge.textContent = currentGameState;
      if (sumoScore) sumoScore.textContent = data.game ? (data.game.score || 0).toLocaleString() : 0;
      if (sumoRateLabel) sumoRateLabel.textContent = `${b.hz || 0} Hz`;

      const stats = (data.game && data.game.stats) ? data.game.stats : {};
      const kos = typeof stats.knockouts === 'number' ? stats.knockouts : 0;
      const opps = typeof stats.opponentsRemaining === 'number' ? stats.opponentsRemaining : 0;
      const wave = typeof stats.wave === 'number' ? stats.wave : 1;
      const integrity = typeof stats.ringIntegrity === 'number' ? stats.ringIntegrity : 100;
      const ready = stats.shockwaveReady !== false;
      const jerk = typeof stats.jerk === 'number' ? stats.jerk : 0;

      if (sumoKnockouts) sumoKnockouts.textContent = kos;
      if (sumoOpponents) sumoOpponents.textContent = opps;
      if (sumoWave) sumoWave.textContent = wave;
      if (sumoRingIntegrity) {
        sumoRingIntegrity.textContent = `${Math.round(integrity)}%`;
        sumoRingIntegrity.style.color = integrity > 50 ? '#34d399' : (integrity > 25 ? '#fbbf24' : '#ef4444');
      }

      if (sumoShockwaveBadge) {
        if (ready) {
          sumoShockwaveBadge.innerHTML = '<span style="color:#fbbf24; font-weight:900;">⚡ READY</span>';
          if (btnSumoShockwave) {
            btnSumoShockwave.style.opacity = '1.0';
            btnSumoShockwave.style.filter = 'none';
          }
        } else {
          const cd = typeof stats.shockwaveCooldown === 'number' ? stats.shockwaveCooldown.toFixed(1) : '...';
          sumoShockwaveBadge.innerHTML = `<span style="color:#94a3b8; font-weight:700;">⏳ ${cd}s</span>`;
          if (btnSumoShockwave) {
            btnSumoShockwave.style.opacity = '0.5';
            btnSumoShockwave.style.filter = 'grayscale(0.6)';
          }
        }
      }

      if (sumoJerkLabel) {
        sumoJerkLabel.textContent = `JERK: ${Math.round(jerk)}°/s²`;
        sumoJerkLabel.style.color = jerk > 150 ? '#ef4444' : (jerk > 80 ? '#facc15' : '#38bdf8');
      }

      if (stats.difficulty && typeof stats.difficulty === 'string') {
        const d = stats.difficulty.toLowerCase();
        sumoDifficulty = d;
        if (sumoDiffNovice) sumoDiffNovice.classList.toggle('active', d === 'novice');
        if (sumoDiffYokozuna) sumoDiffYokozuna.classList.toggle('active', d === 'yokozuna');
      }

      if (stats.arenaSize && typeof stats.arenaSize === 'string') {
        const s = stats.arenaSize.toLowerCase();
        sumoArenaSize = s;
        if (sumoSizeCompact) sumoSizeCompact.classList.toggle('active', s === 'compact');
        if (sumoSizeStandard) sumoSizeStandard.classList.toggle('active', s === 'standard');
        if (sumoSizeColosseum) sumoSizeColosseum.classList.toggle('active', s === 'colosseum');
      }

      if (stats.shockwaveActive && !sumoShockwaveAnim.active) {
        sumoShockwaveAnim = { active: true, radius: 10, alpha: 1.0 };
      }

      drawSumoRadar(stats, normX, normY);
    }

    function drawSumoRadar(stats, normX, normY) {
      const canvas = document.getElementById('wsRadarCanvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      const w = canvas.width;
      const h = canvas.height;
      const cx = w / 2;
      const cy = h / 2;

      ctx.clearRect(0, 0, w, h);

      const maxRadiusM = stats.maxRingRadius || (sumoArenaSize === 'compact' ? 10.5 : (sumoArenaSize === 'colosseum' ? 18.0 : 14.0));
      const currentRadiusM = stats.ringRadius || (maxRadiusM * ((stats.ringIntegrity || 100) / 100));
      const pixelMaxR = 95;
      const scale = pixelMaxR / maxRadiusM;
      const currentPixelR = Math.max(15, currentRadiusM * scale);

      // 1. Danger Abyss Grid
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.04)';
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.arc(cx, cy, pixelMaxR + 10, 0, Math.PI * 2);
      ctx.stroke();

      // 2. Dojo Platform Floor
      const floorGrad = ctx.createRadialGradient(cx, cy, 0, cx, cy, currentPixelR);
      floorGrad.addColorStop(0, '#1e1b4b');
      floorGrad.addColorStop(0.7, '#172554');
      floorGrad.addColorStop(1, '#0f172a');
      ctx.fillStyle = floorGrad;
      ctx.beginPath();
      ctx.arc(cx, cy, currentPixelR, 0, Math.PI * 2);
      ctx.fill();

      // Concentric rings
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.08)';
      ctx.lineWidth = 1;
      ctx.beginPath();
      ctx.arc(cx, cy, currentPixelR * 0.5, 0, Math.PI * 2);
      ctx.stroke();

      // Center crosshair
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.12)';
      ctx.beginPath();
      ctx.moveTo(cx - 8, cy); ctx.lineTo(cx + 8, cy);
      ctx.moveTo(cx, cy - 8); ctx.lineTo(cx, cy + 8);
      ctx.stroke();

      // 3. Ring Perimeter
      const integrity = stats.ringIntegrity || 100;
      ctx.save();
      ctx.lineWidth = integrity < 40 ? 3 : 2;
      ctx.strokeStyle = integrity < 40 ? '#ef4444' : '#38bdf8';
      ctx.shadowColor = integrity < 40 ? '#ef4444' : '#38bdf8';
      ctx.shadowBlur = integrity < 40 ? 12 : 6;
      ctx.setLineDash(integrity < 40 ? [6, 4] : []);
      ctx.beginPath();
      ctx.arc(cx, cy, currentPixelR, 0, Math.PI * 2);
      ctx.stroke();
      ctx.restore();

      // 4. Opponents Markers
      const opponents = Array.isArray(stats.opponents) ? stats.opponents : [];
      opponents.forEach(opp => {
        if (!opp || opp.alive === false) return;
        const ox = cx + (opp.x || 0) * scale;
        const oy = cy + (opp.y || 0) * scale;
        const isHeavy = (opp.mass || 1.0) >= 1.5 || opp.kind === 'heavy';
        const oppR = isHeavy ? 7.5 : 5.0;

        ctx.save();
        ctx.fillStyle = isHeavy ? '#ef4444' : (opp.mass > 0.8 ? '#f59e0b' : '#10b981');
        ctx.shadowColor = ctx.fillStyle;
        ctx.shadowBlur = 8;
        ctx.beginPath();
        ctx.arc(ox, oy, oppR, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      });

      // 5. Player Marker
      let px = cx;
      let py = cy;
      if (stats.playerPos && typeof stats.playerPos.x === 'number') {
        px = cx + stats.playerPos.x * scale;
        py = cy + stats.playerPos.y * scale;
      } else {
        px = cx + (normX || 0) * (currentPixelR * 0.7);
        py = cy + (normY || 0) * (currentPixelR * 0.7);
      }

      // Drive vector line
      const vecX = (normX || 0) * 22;
      const vecY = (normY || 0) * 22;
      ctx.strokeStyle = '#38bdf8';
      ctx.lineWidth = 2.5;
      ctx.beginPath();
      ctx.moveTo(px, py);
      ctx.lineTo(px + vecX, py + vecY);
      ctx.stroke();

      // Hero dot
      ctx.save();
      ctx.fillStyle = '#38bdf8';
      ctx.shadowColor = '#00f0ff';
      ctx.shadowBlur = 10;
      ctx.beginPath();
      ctx.arc(px, py, 6.5, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = '#ffffff';
      ctx.beginPath();
      ctx.arc(px, py, 2.5, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();

      // 6. Shockwave Blast Animation Ripple
      if (sumoShockwaveAnim.active) {
        ctx.save();
        ctx.strokeStyle = `rgba(245, 158, 11, ${sumoShockwaveAnim.alpha})`;
        ctx.lineWidth = 3.5;
        ctx.shadowColor = '#f59e0b';
        ctx.shadowBlur = 14;
        ctx.beginPath();
        ctx.arc(px, py, sumoShockwaveAnim.radius, 0, Math.PI * 2);
        ctx.stroke();
        ctx.restore();

        sumoShockwaveAnim.radius += 5.5;
        sumoShockwaveAnim.alpha -= 0.055;
        if (sumoShockwaveAnim.alpha <= 0 || sumoShockwaveAnim.radius > 80) {
          sumoShockwaveAnim.active = false;
        }
      }
    }

    function setSumoDifficulty(diff) {
      sumoDifficulty = diff.toLowerCase();
      if (sumoDiffNovice) sumoDiffNovice.classList.toggle('active', sumoDifficulty === 'novice');
      if (sumoDiffYokozuna) sumoDiffYokozuna.classList.toggle('active', sumoDifficulty === 'yokozuna');
      sendWsMessage({
        type: 'set_sumo_difficulty',
        difficulty: sumoDifficulty,
        value: sumoDifficulty
      });
    }

    function setSumoArenaSize(size) {
      sumoArenaSize = size.toLowerCase();
      if (sumoSizeCompact) sumoSizeCompact.classList.toggle('active', sumoArenaSize === 'compact');
      if (sumoSizeStandard) sumoSizeStandard.classList.toggle('active', sumoArenaSize === 'standard');
      if (sumoSizeColosseum) sumoSizeColosseum.classList.toggle('active', sumoArenaSize === 'colosseum');
      sendWsMessage({
        type: 'set_sumo_arena_size',
        arenaSize: sumoArenaSize,
        size: sumoArenaSize,
        value: sumoArenaSize
      });
    }

    function setSumoConfig(difficulty, arenaSize) {
      sumoDifficulty = difficulty.toLowerCase();
      sumoArenaSize = arenaSize.toLowerCase();
      sendWsMessage({
        type: 'set_sumo_config',
        difficulty: sumoDifficulty,
        arenaSize: sumoArenaSize
      });
    }

    function triggerShockwave() {
      sumoShockwaveAnim = { active: true, radius: 10, alpha: 1.0 };
      sendWsMessage({ type: 'trigger_shockwave' });
    }

