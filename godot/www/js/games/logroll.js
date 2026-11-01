// River Log Roll Cockpit
    const viewRiverLogRoll = document.getElementById('view-RiverLogRoll');
    const rlDiffBadge = document.getElementById('rlDiffBadge');
    const rlScore = document.getElementById('rlScore');
    const rlRiverSpeed = document.getElementById('rlRiverSpeed');
    const rlLogRpm = document.getElementById('rlLogRpm');
    const rlMultiplier = document.getElementById('rlMultiplier');
    const rlSalmon = document.getElementById('rlSalmon');
    const rlBalance = document.getElementById('rlBalance');
    const rlRateLabel = document.getElementById('rlRateLabel');
    const rlDiffCalm = document.getElementById('rlDiffCalm');
    const rlDiffClass3 = document.getElementById('rlDiffClass3');
    const rlDiffClass5 = document.getElementById('rlDiffClass5');
    const rlSensitivitySlider = document.getElementById('rlSensitivitySlider');
    const rlSensitivityText = document.getElementById('rlSensitivityText');
    const rlBtnInvertRoll = document.getElementById('rlBtnInvertRoll');
    const rlBtnInvertPitch = document.getElementById('rlBtnInvertPitch');

    let riverDifficulty = 'rapids_class_3';
    let logSpinAngle = 0;


    function renderLogRollTelemetry(data, b, normX, normY, rollDeg, pitchDeg) {
      if (rlScore) rlScore.textContent = data.game ? (data.game.score || 0).toLocaleString() : 0;
      if (rlRateLabel) rlRateLabel.textContent = `${b.hz || 0} Hz`;

      const stats = (data.game && data.game.stats) ? data.game.stats : {};
      const spd = typeof stats.riverSpeedKmh === 'number' ? stats.riverSpeedKmh : (typeof stats.riverSpeed === 'number' ? stats.riverSpeed : 24);
      const rpm = typeof stats.logRpm === 'number' ? stats.logRpm : 45;
      const mult = typeof stats.multiplier === 'number' ? stats.multiplier : 1.0;
      const dev = typeof stats.balanceErrorDeg === 'number' ? stats.balanceErrorDeg : (typeof stats.equilibriumDev === 'number' ? stats.equilibriumDev : 0.0);
      const salmon = typeof stats.salmonCollected === 'number' ? stats.salmonCollected : 0;
      const isSweet = typeof stats.isInSweetSpot === 'boolean' ? stats.isInSweetSpot : Math.abs(dev) <= 4.0;
      const isSlip = !!stats.isSlipping;
      const diff = stats.difficulty || 'rapids_class_3';

      if (rlRiverSpeed) rlRiverSpeed.textContent = `${spd} km/h`;
      if (rlLogRpm) rlLogRpm.textContent = `${rpm} RPM`;
      if (rlMultiplier) {
        rlMultiplier.textContent = `${mult.toFixed(1)}x`;
        rlMultiplier.style.color = mult >= 3.0 ? '#facc15' : (mult > 1.2 ? '#10b981' : '#38bdf8');
      }
      if (rlSalmon) rlSalmon.textContent = salmon;
      if (rlDiffBadge) rlDiffBadge.textContent = diff.replace(/_/g, ' ').toUpperCase();

      if (rlBalance) {
        if (isSweet) {
          rlBalance.textContent = 'SWEET SPOT ⚖️';
          rlBalance.style.color = '#34d399';
        } else if (isSlip || Math.abs(dev) > 4.0) {
          rlBalance.textContent = `SLIPPING ⚠️ (${Math.abs(dev).toFixed(1)}°)`;
          rlBalance.style.color = '#fbbf24';
        } else {
          rlBalance.textContent = 'BALANCED';
          rlBalance.style.color = '#38bdf8';
        }
      }

      if (rlDiffCalm) rlDiffCalm.className = diff === 'calm_creek' ? 'btn-toggle active' : 'btn-toggle';
      if (rlDiffClass3) rlDiffClass3.className = diff === 'rapids_class_3' ? 'btn-toggle active' : 'btn-toggle';
      if (rlDiffClass5) rlDiffClass5.className = diff === 'white_water_class_5' ? 'btn-toggle active' : 'btn-toggle';

      drawRiverRadar(stats, normX, normY, rollDeg, dev, rpm, spd);
    }

    function drawRiverRadar(stats, normX, normY, rollDeg, dev, rpm, spd) {
      const canvas = document.getElementById('rlRiverCanvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      const w = canvas.width;
      const h = canvas.height;

      ctx.clearRect(0, 0, w, h);

      // Section 1: Log Rotational Cross-Section (Left: cx = 68, cy = 100)
      const lcx = 68;
      const lcy = 100;
      const logR = 48;
      logSpinAngle += (rpm * 0.002);

      // Bark outer ring
      ctx.save();
      ctx.strokeStyle = '#78350f';
      ctx.lineWidth = 6;
      ctx.beginPath();
      ctx.arc(lcx, lcy, logR, 0, Math.PI * 2);
      ctx.stroke();

      // Wood grain circular end
      const woodGrad = ctx.createRadialGradient(lcx, lcy, 5, lcx, lcy, logR - 3);
      woodGrad.addColorStop(0, '#fde68a');
      woodGrad.addColorStop(0.6, '#d97706');
      woodGrad.addColorStop(1, '#b45309');
      ctx.fillStyle = woodGrad;
      ctx.beginPath();
      ctx.arc(lcx, lcy, logR - 3, 0, Math.PI * 2);
      ctx.fill();

      // Rotating growth rings
      ctx.save();
      ctx.translate(lcx, lcy);
      ctx.rotate(logSpinAngle);
      ctx.strokeStyle = 'rgba(120, 53, 15, 0.4)';
      ctx.lineWidth = 1.5;
      for (let r = 12; r < logR - 6; r += 10) {
        ctx.beginPath();
        ctx.arc(0, 0, r, 0, Math.PI * 2);
        ctx.stroke();
      }
      // Radial tree grain crack
      ctx.beginPath();
      ctx.moveTo(0, 0);
      ctx.lineTo(logR - 6, 0);
      ctx.stroke();
      ctx.restore();

      // Sweet Spot Equilibrium Arc (Top arc: +/- 20 deg)
      ctx.strokeStyle = '#10b981';
      ctx.lineWidth = 4;
      ctx.shadowColor = '#10b981';
      ctx.shadowBlur = 8;
      ctx.beginPath();
      ctx.arc(lcx, lcy, logR + 4, -Math.PI / 2 - 0.35, -Math.PI / 2 + 0.35);
      ctx.stroke();
      ctx.shadowBlur = 0;

      // Rider Balance Bubble Puck
      const riderAngle = -Math.PI / 2 + (dev * Math.PI) / 180;
      const rx = lcx + Math.cos(riderAngle) * (logR + 8);
      const ry = lcy + Math.sin(riderAngle) * (logR + 8);

      ctx.fillStyle = Math.abs(dev) <= 4.0 ? '#34d399' : (Math.abs(dev) <= 12.0 ? '#fbbf24' : '#ef4444');
      ctx.shadowColor = ctx.fillStyle;
      ctx.shadowBlur = 10;
      ctx.beginPath();
      ctx.arc(rx, ry, 6, 0, Math.PI * 2);
      ctx.fill();
      ctx.shadowBlur = 0;
      ctx.restore();

      // Section 2: White-Water Rapids Radar (Right: 145..325)
      const rLeft = 145;
      const rWidth = 180;
      const rTop = 10;
      const rHeight = 180;
      const rMidX = rLeft + rWidth / 2;

      // River Water Background
      ctx.fillStyle = '#0284c7';
      ctx.fillRect(rLeft, rTop, rWidth, rHeight);

      // Canyon Rocky Banks
      ctx.fillStyle = '#334155';
      ctx.fillRect(rLeft, rTop, 16, rHeight);
      ctx.fillRect(rLeft + rWidth - 16, rTop, 16, rHeight);

      // Foaming River Current Streaks
      const riverTime = Date.now() * 0.003;
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.45)';
      ctx.lineWidth = 1.5;
      for (let i = 0; i < 6; i++) {
        const sx = rLeft + 26 + (i * 24);
        const sy = rTop + ((i * 35 + riverTime * 70) % rHeight);
        ctx.beginPath();
        ctx.moveTo(sx, sy);
        ctx.lineTo(sx + (Math.sin(i + riverTime) * 6), sy + 18);
        ctx.stroke();
      }

      // Obstacles (Granite Boulders, Whirlpools, Salmon)
      const obstacles = Array.isArray(stats.obstacles) ? stats.obstacles : [];
      obstacles.forEach(ob => {
        const ox = rMidX + (ob.x || 0) * (rWidth * 0.38);
        const oy = rTop + (ob.y || 0) * rHeight;

        if (ob.type === 'boulder') {
          ctx.save();
          ctx.fillStyle = '#64748b';
          ctx.strokeStyle = '#475569';
          ctx.lineWidth = 2;
          ctx.beginPath();
          ctx.arc(ox, oy, 8, 0, Math.PI * 2);
          ctx.fill();
          ctx.stroke();
          ctx.restore();
        } else if (ob.type === 'whirlpool') {
          ctx.save();
          ctx.strokeStyle = '#00f0ff';
          ctx.lineWidth = 2;
          ctx.beginPath();
          ctx.arc(ox, oy, 7, 0, Math.PI * 1.5);
          ctx.stroke();
          ctx.restore();
        } else if (ob.type === 'salmon') {
          ctx.font = '12px sans-serif';
          ctx.textAlign = 'center';
          ctx.fillText('🐟', ox, oy + 4);
        }
      });

      // The Floating Timber Log
      const logY = rTop + rHeight - 35;
      const logX = rMidX + (normX || 0) * (rWidth * 0.34);
      const logLen = 42;
      const logThick = 12;

      ctx.save();
      ctx.translate(logX, logY);
      ctx.rotate((rollDeg * Math.PI) / 180 * 0.3);

      ctx.fillStyle = '#b45309';
      ctx.strokeStyle = '#78350f';
      ctx.lineWidth = 2;
      ctx.fillRect(-logLen / 2, -logThick / 2, logLen, logThick);
      ctx.strokeRect(-logLen / 2, -logThick / 2, logLen, logThick);

      ctx.fillStyle = '#f8fafc';
      ctx.beginPath();
      ctx.arc(0, -logThick / 2 - 3, 4, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }

    function setRiverDifficulty(diff) {
      riverDifficulty = diff.toLowerCase();
      if (rlDiffCalm) rlDiffCalm.className = riverDifficulty === 'calm_creek' ? 'btn-toggle active' : 'btn-toggle';
      if (rlDiffClass3) rlDiffClass3.className = riverDifficulty === 'rapids_class_3' ? 'btn-toggle active' : 'btn-toggle';
      if (rlDiffClass5) rlDiffClass5.className = riverDifficulty === 'white_water_class_5' ? 'btn-toggle active' : 'btn-toggle';

      sendWsMessage({ type: 'set_river_diff', diff: riverDifficulty, difficulty: riverDifficulty });
    }

