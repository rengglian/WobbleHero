// Downhill Slalom Cockpit
    const viewDownhillSlalom = document.getElementById('view-DownhillSlalom');
    const dsCourseBadge = document.getElementById('dsCourseBadge');
    const dsScore = document.getElementById('dsScore');
    const dsSpeed = document.getElementById('dsSpeed');
    const dsDistance = document.getElementById('dsDistance');
    const dsMultiplier = document.getElementById('dsMultiplier');
    const dsGates = document.getElementById('dsGates');
    const dsSnow = document.getElementById('dsSnow');
    const dsTuckBadge = document.getElementById('dsTuckBadge');
    const dsRateLabel = document.getElementById('dsRateLabel');
    const dsSensitivitySlider = document.getElementById('dsSensitivitySlider');
    const dsSensitivityText = document.getElementById('dsSensitivityText');
    const dsBtnInvertRoll = document.getElementById('dsBtnInvertRoll');
    const dsBtnInvertPitch = document.getElementById('dsBtnInvertPitch');
    const btnCourseSlalom = document.getElementById('btnCourseSlalom');
    const btnCourseGS = document.getElementById('btnCourseGS');
    const btnCourseDownhill = document.getElementById('btnCourseDownhill');
    const btnSnowPowder = document.getElementById('btnSnowPowder');
    const btnSnowIce = document.getElementById('btnSnowIce');


    function renderSlalomTelemetry(data, b, normX, normY, rollDeg, pitchDeg) {
      if (dsScore) dsScore.textContent = data.game ? (data.game.score || 0).toLocaleString() : 0;
      if (dsRateLabel) dsRateLabel.textContent = `${b.hz || 0} Hz`;

      const stats = (data.game && data.game.stats) ? data.game.stats : {};
      const spd = typeof stats.speedKmh === 'number' ? stats.speedKmh : 0;
      const dist = typeof stats.distance === 'number' ? stats.distance : 0;
      const mult = typeof stats.multiplier === 'number' ? stats.multiplier : 1.0;
      const cleared = typeof stats.gatesCleared === 'number' ? stats.gatesCleared : 0;
      const missed = typeof stats.gatesMissed === 'number' ? stats.gatesMissed : 0;
      const course = stats.courseType || 'giant_slalom';
      const snow = stats.snowCondition || 'powder';
      const isTuck = !!stats.isTucking;

      if (dsSpeed) {
        dsSpeed.textContent = `${spd} km/h`;
        dsSpeed.style.color = isTuck ? '#10b981' : (spd > 90 ? '#facc15' : '#38bdf8');
      }
      if (dsDistance) dsDistance.textContent = `${dist.toLocaleString()}m`;
      if (dsMultiplier) {
        dsMultiplier.textContent = `${mult.toFixed(1)}x`;
        dsMultiplier.style.color = mult >= 3.0 ? '#f43f5e' : (mult > 1.2 ? '#facc15' : '#38bdf8');
      }
      if (dsGates) dsGates.textContent = `${cleared} / ${cleared + missed}`;
      if (dsSnow) dsSnow.textContent = snow.toUpperCase();
      if (dsCourseBadge) dsCourseBadge.textContent = course.replace('_', ' ').toUpperCase();

      if (dsTuckBadge) {
        dsTuckBadge.style.display = isTuck ? 'inline-block' : 'none';
      }

      drawSlalomRadar(stats, normX, rollDeg, spd, isTuck);
    }

    function drawSlalomRadar(stats, normX, rollDeg, speedKmh, isTuck) {
      const canvas = document.getElementById('dsSlopeCanvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      const w = canvas.width;
      const h = canvas.height;

      ctx.clearRect(0, 0, w, h);

      // Piste scroll animation
      slalomPisteOffset = (slalomPisteOffset + (speedKmh / 3.6) * 0.15) % 40;

      // 1. Alpine Piste perspective boundaries
      const topWidth = 140;
      const bottomWidth = 260;
      const topX = (w - topWidth) / 2;
      const bottomX = (w - bottomWidth) / 2;

      ctx.save();
      // Snow piste surface
      ctx.fillStyle = '#0f172a';
      ctx.beginPath();
      ctx.moveTo(topX, 10);
      ctx.lineTo(topX + topWidth, 10);
      ctx.lineTo(bottomX + bottomWidth, h - 10);
      ctx.lineTo(bottomX, h - 10);
      ctx.closePath();
      ctx.fill();

      // Piste outer snowbanks
      ctx.strokeStyle = '#38bdf8';
      ctx.lineWidth = 2;
      ctx.shadowColor = '#38bdf8';
      ctx.shadowBlur = 6;
      ctx.stroke();
      ctx.restore();

      // 2. Snow contour lines
      ctx.save();
      ctx.strokeStyle = 'rgba(56, 189, 248, 0.15)';
      ctx.lineWidth = 1;
      for (let y = 15; y < h - 10; y += 30) {
        const py = (y + slalomPisteOffset) % (h - 25) + 10;
        const progress = (py - 10) / (h - 20);
        const curW = topWidth + (bottomWidth - topWidth) * progress;
        const curX = (w - curW) / 2;
        ctx.beginPath();
        ctx.moveTo(curX, py);
        ctx.lineTo(curX + curW, py);
        ctx.stroke();
      }
      ctx.restore();

      // 3. Roadside Pine Trees
      ctx.save();
      ctx.fillStyle = '#059669';
      for (let y = 20; y < h; y += 45) {
        const ty = (y + slalomPisteOffset) % (h - 15) + 15;
        const progress = (ty - 10) / (h - 20);
        const curW = topWidth + (bottomWidth - topWidth) * progress;
        const lx = (w - curW) / 2 - 14;
        const rx = (w + curW) / 2 + 14;
        const treeSize = 6 + progress * 6;

        // Left pine
        ctx.beginPath();
        ctx.moveTo(lx, ty - treeSize);
        ctx.lineTo(lx - treeSize * 0.7, ty + treeSize);
        ctx.lineTo(lx + treeSize * 0.7, ty + treeSize);
        ctx.fill();

        // Right pine
        ctx.beginPath();
        ctx.moveTo(rx, ty - treeSize);
        ctx.lineTo(rx - treeSize * 0.7, ty + treeSize);
        ctx.lineTo(rx + treeSize * 0.7, ty + treeSize);
        ctx.fill();
      }
      ctx.restore();

      // 4. Slalom Gates cascading down
      for (let i = 0; i < 3; i++) {
        const gy = ((i * 65 + slalomPisteOffset * 1.5) % (h - 35)) + 15;
        const progress = (gy - 10) / (h - 20);
        const curW = topWidth + (bottomWidth - topWidth) * progress;
        const gateSpan = 22 + progress * 20;
        const isRed = (i % 2 === 0);
        const gateColor = isRed ? '#ef4444' : '#3b82f6';
        const waveX = Math.sin(i * 1.8 + (stats.distance || 0) * 0.05) * (curW * 0.28);
        const gx = w / 2 + waveX;

        ctx.save();
        ctx.strokeStyle = gateColor;
        ctx.fillStyle = gateColor;
        ctx.shadowColor = gateColor;
        ctx.shadowBlur = 8;
        ctx.lineWidth = 2.5;

        // Left Flag Pole
        ctx.beginPath();
        ctx.arc(gx - gateSpan / 2, gy, 3, 0, Math.PI * 2);
        ctx.fill();

        // Right Flag Pole
        ctx.beginPath();
        ctx.arc(gx + gateSpan / 2, gy, 3, 0, Math.PI * 2);
        ctx.fill();

        // Connecting Banner Ribbon
        ctx.beginPath();
        ctx.moveTo(gx - gateSpan / 2, gy);
        ctx.lineTo(gx + gateSpan / 2, gy);
        ctx.stroke();
        ctx.restore();
      }

      // 5. Skier Avatar
      const skierX = w / 2 + (normX * (bottomWidth * 0.42));
      const skierY = h - 35;

      ctx.save();
      // Tuck speed glow
      if (isTuck) {
        ctx.shadowColor = '#10b981';
        ctx.shadowBlur = 15;
      }

      // Twin Skis
      const skiLean = (rollDeg / 25) * 6;
      ctx.fillStyle = '#ef4444';
      ctx.fillRect(skierX - 6, skierY - 10 + skiLean, 3, 20);
      ctx.fillRect(skierX + 3, skierY - 10 - skiLean, 3, 20);

      // Skier Body
      ctx.fillStyle = isTuck ? '#10b981' : '#38bdf8';
      ctx.beginPath();
      ctx.arc(skierX, skierY, 7, 0, Math.PI * 2);
      ctx.fill();

      // Helmet visor
      ctx.fillStyle = '#facc15';
      ctx.beginPath();
      ctx.arc(skierX, skierY - 2, 4, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
    }

    function setSlalomCourse(course) {
      if (btnCourseSlalom) btnCourseSlalom.className = course === 'slalom' ? 'btn-toggle active' : 'btn-toggle';
      if (btnCourseGS) btnCourseGS.className = course === 'giant_slalom' ? 'btn-toggle active' : 'btn-toggle';
      if (btnCourseDownhill) btnCourseDownhill.className = course === 'downhill' ? 'btn-toggle active' : 'btn-toggle';

      sendWsMessage({ type: 'set_course', course: course });
    }

    function setSlalomSnow(snow) {
      if (btnSnowPowder) btnSnowPowder.className = snow === 'powder' ? 'btn-toggle active' : 'btn-toggle';
      if (btnSnowIce) btnSnowIce.className = snow === 'ice' ? 'btn-toggle active' : 'btn-toggle';

      sendWsMessage({ type: 'set_snow', snow: snow });
    }

    // River Log Roll Telemetry & Radar
