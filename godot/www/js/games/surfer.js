// Asteroid Surfer Cockpit
    const viewAsteroidSurfer = document.getElementById('view-AsteroidSurfer');
    const asStateBadge = document.getElementById('asStateBadge');
    const asScore = document.getElementById('asScore');
    const asSpeed = document.getElementById('asSpeed');
    const asMultiplier = document.getElementById('asMultiplier');
    const asAirTime = document.getElementById('asAirTime');
    const asCosmicDust = document.getElementById('asCosmicDust');
    const asLipHeight = document.getElementById('asLipHeight');
    const asRateLabel = document.getElementById('asRateLabel');
    const asWaveStatusBadge = document.getElementById('asWaveStatusBadge');
    const asBankDeg = document.getElementById('asBankDeg');
    const asAltitudeVal = document.getElementById('asAltitudeVal');
    const asWaveSpeedSlider = document.getElementById('asWaveSpeedSlider');
    const asWaveSpeedText = document.getElementById('asWaveSpeedText');
    const asDensityLow = document.getElementById('asDensityLow');
    const asDensityMedium = document.getElementById('asDensityMedium');
    const asDensityHigh = document.getElementById('asDensityHigh');
    const asSensitivitySlider = document.getElementById('asSensitivitySlider');
    const asSensitivityText = document.getElementById('asSensitivityText');

    function drawSurfVisualizer(normX, normY, rollDeg, pitchDeg, stats) {
      const canvas = document.getElementById('asSurfCanvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      const w = canvas.width;
      const h = canvas.height;

      ctx.clearRect(0, 0, w, h);

      // 1. Subtle horizontal wave height guide lines
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.05)';
      ctx.lineWidth = 1;
      for (let y = 30; y < h; y += 35) {
        ctx.beginPath();
        ctx.moveTo(0, y);
        ctx.lineTo(w, y);
        ctx.stroke();
      }

      // 2. Cosmic Wave cross-section curve
      const y0 = 145;
      const xLip = 285, yLip = 38;
      const cp1x = 130, cp1y = 150;
      const cp2x = 210, cp2y = 45;

      // Glowing plasma gradient bulk under wave
      const waveGrad = ctx.createLinearGradient(0, yLip, 0, h);
      waveGrad.addColorStop(0, 'rgba(168, 85, 247, 0.35)');
      waveGrad.addColorStop(0.5, 'rgba(56, 189, 248, 0.2)');
      waveGrad.addColorStop(1, 'rgba(15, 23, 42, 0.6)');

      ctx.beginPath();
      ctx.moveTo(0, h);
      ctx.lineTo(0, y0);
      ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, xLip, yLip);
      ctx.lineTo(w, yLip - 10);
      ctx.lineTo(w, h);
      ctx.closePath();
      ctx.fillStyle = waveGrad;
      ctx.fill();

      // Neon cyan wave crest line
      ctx.beginPath();
      ctx.moveTo(0, y0);
      ctx.bezierCurveTo(cp1x, cp1y, cp2x, cp2y, xLip, yLip);
      ctx.lineTo(w, yLip - 10);
      ctx.strokeStyle = '#38bdf8';
      ctx.lineWidth = 3;
      ctx.shadowColor = '#00f0ff';
      ctx.shadowBlur = 10;
      ctx.stroke();
      ctx.shadowBlur = 0;

      // Zero-G Lip ejection threshold line
      ctx.setLineDash([4, 4]);
      ctx.strokeStyle = 'rgba(244, 114, 182, 0.6)';
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.moveTo(xLip, yLip - 20);
      ctx.lineTo(xLip, h);
      ctx.stroke();
      ctx.setLineDash([]);

      ctx.fillStyle = '#f472b6';
      ctx.font = 'bold 9px sans-serif';
      ctx.fillText('ZERO-G LIP', xLip - 28, yLip - 16);

      // 3. Surfer Board Position & Banking
      let wavePos = (stats && typeof stats.wavePosition === 'number') 
        ? stats.wavePosition 
        : (normX || 0); // -1..1
      
      const t = Math.max(0.08, Math.min(0.96, (wavePos + 1) / 2));

      const p0x = 0, p0y = y0;
      const p1x = cp1x, p1y = cp1y;
      const p2x = cp2x, p2y = cp2y;
      const p3x = xLip, p3y = yLip;

      const mt = 1 - t;
      let px = mt*mt*mt*p0x + 3*mt*mt*t*p1x + 3*mt*t*t*p2x + t*t*t*p3x;
      let py = mt*mt*mt*p0y + 3*mt*mt*t*p1y + 3*mt*t*t*p2y + t*t*t*p3y;

      const dx = 3*mt*mt*(p1x - p0x) + 6*mt*t*(p2x - p1x) + 3*t*t*(p3x - p2x);
      const dy = 3*mt*mt*(p1y - p0y) + 6*mt*t*(p2y - p1y) + 3*t*t*(p3y - p2y);
      const waveSlopeAngle = Math.atan2(dy, dx);

      const isAirborne = stats && (stats.isAirborne || (stats.airTime && stats.airTime > 0));
      const airTime = (stats && stats.airTime) || 0;

      if (isAirborne) {
        const jumpH = Math.min(65, airTime * 45);
        py -= jumpH;
      }

      const bankRad = (rollDeg * Math.PI) / 180;
      const totalRotation = waveSlopeAngle + (bankRad * 0.7);

      // Draw Surfer
      ctx.save();
      ctx.translate(px, py);
      ctx.rotate(totalRotation);

      // Thruster trail
      ctx.fillStyle = isAirborne ? 'rgba(244, 114, 182, 0.8)' : 'rgba(56, 189, 248, 0.7)';
      ctx.shadowColor = isAirborne ? '#f472b6' : '#38bdf8';
      ctx.shadowBlur = 8;
      ctx.beginPath();
      ctx.moveTo(-16, -2);
      ctx.lineTo(-24 - Math.random() * 5, 0);
      ctx.lineTo(-16, 2);
      ctx.closePath();
      ctx.fill();

      // Board Hull
      const boardGrad = ctx.createLinearGradient(-15, 0, 15, 0);
      boardGrad.addColorStop(0, '#0284c7');
      boardGrad.addColorStop(0.5, '#e0e7ff');
      boardGrad.addColorStop(1, '#a855f7');
      ctx.fillStyle = boardGrad;
      ctx.beginPath();
      ctx.ellipse(0, 0, 16, 4.5, 0, 0, Math.PI * 2);
      ctx.fill();

      // Surfer Icon
      ctx.shadowBlur = 0;
      ctx.font = '14px sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'bottom';
      ctx.fillText('🏄', 0, -4);

      ctx.restore();

      // Landing feedback
      if (isAirborne) {
        const isLevel = Math.abs(rollDeg) < 4.0;
        ctx.font = 'bold 11px sans-serif';
        ctx.textAlign = 'center';
        ctx.fillStyle = isLevel ? '#34d399' : '#fbbf24';
        ctx.fillText(isLevel ? '⚡ CLEAN LANDING READY ⚡' : '⚠️ LEVEL BOARD FOR CLEAN LANDING', w / 2, 22);
      }
    }

    function onWaveSpeedInput(val) {
      currentWaveSpeed = parseFloat(val);
      if (asWaveSpeedText) asWaveSpeedText.textContent = `${Math.round(currentWaveSpeed)} km/h`;
      sendWsMessage({
        type: 'set_wave_speed',
        speed: currentWaveSpeed,
        value: currentWaveSpeed
      });
    }

    function setCosmicDensity(density) {
      currentCosmicDensity = density.toLowerCase();
      if (asDensityLow) asDensityLow.classList.toggle('active', currentCosmicDensity === 'low');
      if (asDensityMedium) asDensityMedium.classList.toggle('active', currentCosmicDensity === 'medium');
      if (asDensityHigh) asDensityHigh.classList.toggle('active', currentCosmicDensity === 'high');
      sendWsMessage({
        type: 'set_wave_density',
        density: currentCosmicDensity,
        value: currentCosmicDensity
      });
    }

