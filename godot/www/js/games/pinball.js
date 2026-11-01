// Gravity Pinball Cockpit
    const gpScore = document.getElementById('gpScore');
    const gpActiveBalls = document.getElementById('gpActiveBalls');
    const gpReserveBalls = document.getElementById('gpReserveBalls');
    const gpMultiplier = document.getElementById('gpMultiplier');
    const gpBumpersHit = document.getElementById('gpBumpersHit');
    const gpTiltBadge = document.getElementById('gpTiltBadge');
    const gpRateLabel = document.getElementById('gpRateLabel');
    const gpInclineSlider = document.getElementById('gpInclineSlider');
    const gpInclineText = document.getElementById('gpInclineText');
    const gpRestitutionSlider = document.getElementById('gpRestitutionSlider');
    const gpRestitutionText = document.getElementById('gpRestitutionText');
    const gpSensitivitySlider = document.getElementById('gpSensitivitySlider');
    const gpSensitivityText = document.getElementById('gpSensitivityText');
    const gpBtnInvertRoll = document.getElementById('gpBtnInvertRoll');
    const gpBtnInvertPitch = document.getElementById('gpBtnInvertPitch');

    let tableInclineDeg = 12;
    let bumperRestitution = 0.85;
    let nudgeAnimTimer = 0;


    function renderPinballTelemetry(data, b, normX, normY, rollDeg, pitchDeg) {
      if (gpStateBadge) gpStateBadge.textContent = currentGameState;
      if (gpScore) gpScore.textContent = data.game ? (data.game.score || 0).toLocaleString() : 0;
      if (gpRateLabel) gpRateLabel.textContent = `${b.hz || 0} Hz`;

      const stats = (data.game && data.game.stats) ? data.game.stats : {};
      const activeCount = typeof stats.activeBalls === 'number' ? stats.activeBalls : 1;
      const reserveCount = typeof stats.reserveBalls === 'number' ? stats.reserveBalls : 3;
      const mult = typeof stats.multiplier === 'number' ? stats.multiplier : 1.0;
      const hits = typeof stats.bumpersHit === 'number' ? stats.bumpersHit : 0;
      const tiltWarnings = typeof stats.tiltWarnings === 'number' ? stats.tiltWarnings : 0;
      const isTiltLocked = !!stats.isTiltLocked || !!stats.isTilted;

      if (gpActiveBalls) gpActiveBalls.textContent = activeCount;
      if (gpReserveBalls) gpReserveBalls.textContent = reserveCount;
      if (gpMultiplier) gpMultiplier.textContent = `${mult.toFixed(1)}x`;
      if (gpBumpersHit) gpBumpersHit.textContent = hits;

      if (gpTiltBadge) {
        if (isTiltLocked) {
          gpTiltBadge.textContent = '⛔ TILT LOCKED';
          gpTiltBadge.style.color = '#ef4444';
          gpTiltBadge.style.borderColor = 'rgba(239, 68, 68, 0.4)';
        } else if (tiltWarnings > 0) {
          gpTiltBadge.textContent = `⚠️ TILTS: ${tiltWarnings}/3`;
          gpTiltBadge.style.color = '#fbbf24';
          gpTiltBadge.style.borderColor = 'rgba(251, 191, 36, 0.4)';
        } else {
          gpTiltBadge.textContent = 'TILTS: 0/3 (SAFE)';
          gpTiltBadge.style.color = '#10b981';
          gpTiltBadge.style.borderColor = 'rgba(16, 185, 129, 0.3)';
        }
      }

      if (typeof stats.tableIncline === 'number' && document.activeElement !== gpInclineSlider) {
        tableInclineDeg = stats.tableIncline;
        gpInclineSlider.value = tableInclineDeg;
        gpInclineText.textContent = `${Math.round(tableInclineDeg)}°`;
      }

      if (typeof stats.restitution === 'number' && document.activeElement !== gpRestitutionSlider) {
        bumperRestitution = stats.restitution;
        gpRestitutionSlider.value = bumperRestitution;
        gpRestitutionText.textContent = `${bumperRestitution.toFixed(2)}x`;
      }

      // Draw Live 2D Pinball Radar Playfield
      drawPinballRadar(stats, normX, normY);
    }

    function drawPinballRadar(stats, normX, normY) {
      const canvas = document.getElementById('gpRadarCanvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      const w = canvas.width;
      const h = canvas.height;

      ctx.clearRect(0, 0, w, h);

      // Nudge Screen Shake
      ctx.save();
      if (nudgeAnimTimer > 0) {
        nudgeAnimTimer -= 0.05;
        const shakeX = (Math.random() - 0.5) * 6;
        const shakeY = (Math.random() - 0.5) * 6;
        ctx.translate(shakeX, shakeY);
      }

      // Playfield Dimensions (Centered cabinet table: 200w x 220h)
      const tLeft = (w - 200) / 2;
      const tTop = 10;
      const tWidth = 200;
      const tHeight = 220;

      // 1. Playfield Cabinet Boundary
      ctx.fillStyle = '#0f172a';
      ctx.fillRect(tLeft, tTop, tWidth, tHeight);

      // Neon outer perimeter stroke
      ctx.strokeStyle = '#38bdf8';
      ctx.lineWidth = 2.5;
      ctx.shadowColor = '#38bdf8';
      ctx.shadowBlur = 8;
      ctx.beginPath();
      // Outer cabinet walls
      ctx.moveTo(tLeft, tTop + 25);
      ctx.lineTo(tLeft + 25, tTop);
      ctx.lineTo(tLeft + tWidth - 25, tTop);
      ctx.lineTo(tLeft + tWidth, tTop + 25);
      ctx.lineTo(tLeft + tWidth, tTop + tHeight - 40);
      // Bottom outlane funnels with drain gap (width 50px)
      ctx.lineTo(tLeft + tWidth - 30, tTop + tHeight);
      ctx.moveTo(tLeft + 30, tTop + tHeight);
      ctx.lineTo(tLeft, tTop + tHeight - 40);
      ctx.closePath();
      ctx.stroke();
      ctx.shadowBlur = 0;

      // Bottom Drain Gap Hazard Line
      ctx.strokeStyle = '#ef4444';
      ctx.setLineDash([4, 4]);
      ctx.beginPath();
      ctx.moveTo(tLeft + 75, tTop + tHeight);
      ctx.lineTo(tLeft + 125, tTop + tHeight);
      ctx.stroke();
      ctx.setLineDash([]);

      // 2. 4 Drop-Target Banks
      const dropTargets = Array.isArray(stats.targets) ? stats.targets : [
        { id: 1, active: true }, { id: 2, active: true }, { id: 3, active: true }, { id: 4, active: true }
      ];
      const targetW = 22;
      const targetSpacing = 28;
      const startX = tLeft + 48;
      dropTargets.forEach((tg, idx) => {
        const tx = startX + idx * targetSpacing;
        const ty = tTop + 24;
        ctx.save();
        ctx.fillStyle = tg.active ? '#10b981' : '#334155';
        ctx.shadowColor = tg.active ? '#10b981' : 'transparent';
        ctx.shadowBlur = tg.active ? 8 : 0;
        ctx.fillRect(tx, ty, targetW, 5);
        ctx.restore();
      });

      // 3. Neon Bumpers (3 Bumper Cluster)
      const bumpers = [
        { x: tLeft + 100, y: tTop + 65, r: 13, color: '#00f0ff', glow: '#00f0ff' },  // Top
        { x: tLeft + 65,  y: tTop + 105, r: 11, color: '#f43f5e', glow: '#f43f5e' }, // Left
        { x: tLeft + 135, y: tTop + 105, r: 11, color: '#facc15', glow: '#facc15' }  // Right
      ];

      bumpers.forEach(bm => {
        ctx.save();
        ctx.fillStyle = bm.color;
        ctx.shadowColor = bm.glow;
        ctx.shadowBlur = 10;
        ctx.beginPath();
        ctx.arc(bm.x, bm.y, bm.r, 0, Math.PI * 2);
        ctx.fill();
        ctx.fillStyle = '#ffffff';
        ctx.beginPath();
        ctx.arc(bm.x, bm.y, bm.r * 0.45, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      });

      // 4. Active Chrome Pinballs
      const balls = Array.isArray(stats.balls) ? stats.balls : [
        { x: 0.0, y: -0.2, vx: 0.2, vy: 0.4 }
      ];

      balls.forEach(ball => {
        // Map normalized [-1..1, -1..1] to table coordinates
        const bx = tLeft + 100 + (ball.x || 0) * 80;
        const by = tTop + 110 + (ball.y || 0) * 95;

        // Motion trail line
        const vx = (ball.vx || 0) * 8;
        const vy = (ball.vy || 0) * 8;
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.4)';
        ctx.lineWidth = 1.5;
        ctx.beginPath();
        ctx.moveTo(bx, by);
        ctx.lineTo(bx - vx, by - vy);
        ctx.stroke();

        // Chrome Sphere Body
        ctx.save();
        const ballGrad = ctx.createRadialGradient(bx - 2, by - 2, 1, bx, by, 6);
        ballGrad.addColorStop(0, '#ffffff');
        ballGrad.addColorStop(0.4, '#e2e8f0');
        ballGrad.addColorStop(1, '#64748b');
        ctx.fillStyle = ballGrad;
        ctx.shadowColor = '#94a3b8';
        ctx.shadowBlur = 6;
        ctx.beginPath();
        ctx.arc(bx, by, 5.5, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      });

      // 5. Dynamic 2D Gravity Direction Arrow (Top Left)
      const gx = normX * 16;
      const gy = normY * 16 + (tableInclineDeg / 25) * 8; // Baseline table incline downward pull
      const gOriginX = tLeft - 22;
      const gOriginY = tTop + 35;

      ctx.save();
      ctx.strokeStyle = '#a855f7';
      ctx.fillStyle = '#a855f7';
      ctx.lineWidth = 2.5;
      ctx.shadowColor = '#a855f7';
      ctx.shadowBlur = 8;
      ctx.beginPath();
      ctx.moveTo(gOriginX, gOriginY);
      ctx.lineTo(gOriginX + gx, gOriginY + gy);
      ctx.stroke();

      ctx.font = 'bold 8px sans-serif';
      ctx.textAlign = 'center';
      ctx.fillText('GRAVITY', gOriginX, gOriginY - 8);
      ctx.restore();

      ctx.restore(); // Restore shake translation
    }

    function launchPinball() {
      sendWsMessage({ type: 'launch_pinball' });
    }

    function nudgeTable() {
      nudgeAnimTimer = 0.3; // Trigger local canvas shake
      sendWsMessage({ type: 'nudge_table' });
    }

    function onTableInclineInput(val) {
      tableInclineDeg = parseFloat(val);
      if (gpInclineText) gpInclineText.textContent = `${Math.round(tableInclineDeg)}°`;
      sendWsMessage({
        type: 'set_table_incline',
        incline: tableInclineDeg,
        deg: tableInclineDeg,
        value: tableInclineDeg
      });
    }

    function onPinballRestitutionInput(val) {
      bumperRestitution = parseFloat(val);
      if (gpRestitutionText) gpRestitutionText.textContent = `${bumperRestitution.toFixed(2)}x`;
      sendWsMessage({
        type: 'set_pinball_restitution',
        restitution: bumperRestitution,
        value: bumperRestitution
      });
    }

    // Downhill Slalom Telemetry & Radar
    let slalomPisteOffset = 0;

