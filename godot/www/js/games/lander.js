// Lunar Lander Cockpit
    const sumoSizeColosseum = document.getElementById('sumoSizeColosseum');
    const sumoSensitivitySlider = document.getElementById('sumoSensitivitySlider');
    const sumoSensitivityText = document.getElementById('sumoSensitivityText');
    const sumoBtnInvertRoll = document.getElementById('sumoBtnInvertRoll');
    const sumoBtnInvertPitch = document.getElementById('sumoBtnInvertPitch');

    let sumoDifficulty = 'novice';
    let sumoArenaSize = 'standard';
    let sumoShockwaveAnim = { active: false, x: 0, y: 0, radius: 0, alpha: 0 };

    // Lunar Lander Elements
    const viewLunarLander = document.getElementById('view-LunarLander');
    const llStateBadge = document.getElementById('llStateBadge');
    const llScore = document.getElementById('llScore');
    const llFuelPct = document.getElementById('llFuelPct');
    const llFuelFill = document.getElementById('llFuelFill');
    const llVertSpeed = document.getElementById('llVertSpeed');
    const llHorizSpeed = document.getElementById('llHorizSpeed');
    const llAltitude = document.getElementById('llAltitude');
    const llTiltRoll = document.getElementById('llTiltRoll');
    const llEnvelopeBadge = document.getElementById('llEnvelopeBadge');
    const llRateLabel = document.getElementById('llRateLabel');
    const llGravMoon = document.getElementById('llGravMoon');
    const llGravMars = document.getElementById('llGravMars');
    const llGravEuropa = document.getElementById('llGravEuropa');
    const llGravEarth = document.getElementById('llGravEarth');
    const llSensitivitySlider = document.getElementById('llSensitivitySlider');
    const llSensitivityText = document.getElementById('llSensitivityText');
    const llBtnInvertRoll = document.getElementById('llBtnInvertRoll');
    const llBtnInvertPitch = document.getElementById('llBtnInvertPitch');

    let landerGravity = 'moon';

    function renderLanderTelemetry(data, b, normX, normY, rollDeg, pitchDeg) {
      if (llStateBadge) llStateBadge.textContent = currentGameState;
      if (llScore) llScore.textContent = data.game ? (data.game.score || 0).toLocaleString() : 0;
      if (llRateLabel) llRateLabel.textContent = `${b.hz || 0} Hz`;

      const stats = (data.game && data.game.stats) ? data.game.stats : {};
      const fuel = typeof stats.fuel === 'number' ? stats.fuel : 100.0;
      const vy = typeof stats.verticalSpeed === 'number' ? stats.verticalSpeed : 0.0;
      const vx = typeof stats.horizontalSpeed === 'number' ? stats.horizontalSpeed : 0.0;
      const alt = typeof stats.altitude === 'number' ? stats.altitude : 0.0;
      const roll = typeof stats.tiltRoll === 'number' ? stats.tiltRoll : rollDeg;

      // Fuel Meter UI
      if (llFuelPct) llFuelPct.textContent = `${Math.round(fuel)}%`;
      if (llFuelFill) {
        llFuelFill.style.width = `${Math.max(0, Math.min(100, fuel))}%`;
        if (fuel > 35) {
          llFuelFill.style.background = 'linear-gradient(90deg, #0284c7, #38bdf8)';
        } else if (fuel > 15) {
          llFuelFill.style.background = 'linear-gradient(90deg, #d97706, #fbbf24)';
        } else {
          llFuelFill.style.background = 'linear-gradient(90deg, #dc2626, #ef4444)';
        }
      }

      // Vertical Speed (Descent Envelope)
      if (llVertSpeed) {
        const absVy = Math.abs(vy);
        llVertSpeed.textContent = `${vy >= 0 ? '+' : ''}${vy.toFixed(1)} m/s`;
        if (absVy <= 1.8) {
          llVertSpeed.style.color = '#34d399'; // Safe
        } else if (absVy <= 3.2) {
          llVertSpeed.style.color = '#fbbf24'; // Warning
        } else {
          llVertSpeed.style.color = '#ef4444'; // Crash risk
        }
      }

      // Horizontal Speed
      if (llHorizSpeed) {
        const absVx = Math.abs(vx);
        llHorizSpeed.textContent = `${vx >= 0 ? '+' : ''}${vx.toFixed(1)} m/s`;
        llHorizSpeed.style.color = absVx <= 1.0 ? '#38bdf8' : (absVx <= 2.0 ? '#fbbf24' : '#ef4444');
      }

      // Altitude
      if (llAltitude) {
        llAltitude.textContent = `${alt.toFixed(1)}m`;
      }

      // Tilt Roll
      if (llTiltRoll) {
        const absRoll = Math.abs(roll);
        llTiltRoll.textContent = `${roll >= 0 ? '+' : ''}${roll.toFixed(1)}°`;
        llTiltRoll.style.color = absRoll <= 3.5 ? '#34d399' : (absRoll <= 8.0 ? '#fbbf24' : '#ef4444');
      }

      // Touchdown Envelope Indicator
      if (llEnvelopeBadge) {
        const absVy = Math.abs(vy);
        const absVx = Math.abs(vx);
        const absRoll = Math.abs(roll);
        const isSafeSpeed = absVy <= 1.8 && absVx <= 1.5;
        const isSafeTilt = absRoll <= 5.0;
        const overPad = stats.overPad !== false;

        if (currentGameState === 'LANDED' || stats.touchdownStatus === 'SOFT TOUCHDOWN') {
          llEnvelopeBadge.innerHTML = '🎉 SOFT TOUCHDOWN ✅';
          llEnvelopeBadge.style.color = '#34d399';
          llEnvelopeBadge.style.borderColor = 'rgba(52,211,153,0.4)';
        } else if (stats.touchdownStatus === 'HARD TOUCHDOWN') {
          llEnvelopeBadge.innerHTML = '⚠️ HARD TOUCHDOWN';
          llEnvelopeBadge.style.color = '#fbbf24';
          llEnvelopeBadge.style.borderColor = 'rgba(251,191,36,0.4)';
        } else if (currentGameState === 'GAMEOVER' || stats.touchdownStatus === 'CRASHED') {
          llEnvelopeBadge.innerHTML = '💥 CRASH IMPACT';
          llEnvelopeBadge.style.color = '#ef4444';
          llEnvelopeBadge.style.borderColor = 'rgba(239,68,68,0.4)';
        } else if (isSafeSpeed && isSafeTilt && overPad) {
          llEnvelopeBadge.innerHTML = 'TOUCHDOWN ALIGNED ✅';
          llEnvelopeBadge.style.color = '#34d399';
          llEnvelopeBadge.style.borderColor = 'rgba(52,211,153,0.3)';
        } else if (!isSafeSpeed) {
          llEnvelopeBadge.innerHTML = 'EXCESS VELOCITY ⚠️';
          llEnvelopeBadge.style.color = '#fbbf24';
          llEnvelopeBadge.style.borderColor = 'rgba(251,191,36,0.3)';
        } else if (!isSafeTilt) {
          llEnvelopeBadge.innerHTML = 'TILT CRITICAL ⛔';
          llEnvelopeBadge.style.color = '#ef4444';
          llEnvelopeBadge.style.borderColor = 'rgba(239,68,68,0.3)';
        } else {
          llEnvelopeBadge.innerHTML = 'OFF-PAD TRAJECTORY ⚠️';
          llEnvelopeBadge.style.color = '#fbbf24';
          llEnvelopeBadge.style.borderColor = 'rgba(251,191,36,0.3)';
        }
      }

      // Sync active planet buttons
      if (stats.gravityPreset && typeof stats.gravityPreset === 'string') {
        const p = stats.gravityPreset.toLowerCase();
        landerGravity = p;
        if (llGravMoon) llGravMoon.classList.toggle('active', p === 'moon');
        if (llGravMars) llGravMars.classList.toggle('active', p === 'mars');
        if (llGravEuropa) llGravEuropa.classList.toggle('active', p === 'europa');
        if (llGravEarth) llGravEarth.classList.toggle('active', p === 'earth');
      }

      drawLanderRadar(stats, normX, normY, rollDeg, pitchDeg);
    }

    function drawLanderRadar(stats, normX, normY, rollDeg, pitchDeg) {
      const canvas = document.getElementById('llDescentCanvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      const w = canvas.width;
      const h = canvas.height;

      ctx.clearRect(0, 0, w, h);

      // Coordinate Scaling (Playfield x in [-45, 45], y in [0, 38])
      const toScreenX = (x) => ((x + 45) / 90) * w;
      const toScreenY = (y) => h - 22 - (y / 38) * (h - 40);

      // 1. Cavern Grid
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.04)';
      ctx.lineWidth = 1;
      for (let y = 10; y <= 30; y += 10) {
        const sy = toScreenY(y);
        ctx.beginPath();
        ctx.moveTo(0, sy); ctx.lineTo(w, sy);
        ctx.stroke();
      }

      // 2. Terrain Profile
      ctx.fillStyle = '#1e293b';
      ctx.beginPath();
      ctx.moveTo(0, h);
      for (let x = -45; x <= 45; x += 3) {
        let ty = 4.0 + 3.0 * Math.sin(x * 0.08) + 2.2 * Math.cos(x * 0.18) + 1.2 * Math.sin(x * 0.45);
        if (x >= -32 && x <= -22) ty = 5.0; // Pad Alpha
        else if (x >= -5 && x <= 5) ty = 8.5; // Pad Beta
        else if (x >= 24 && x <= 29.5) ty = 14.0; // Pad Gamma
        const sx = toScreenX(x);
        const sy = toScreenY(ty);
        if (x === -45) ctx.lineTo(sx, sy);
        else ctx.lineTo(sx, sy);
      }
      ctx.lineTo(w, h);
      ctx.closePath();
      ctx.fill();

      // Terrain outline
      ctx.strokeStyle = '#475569';
      ctx.lineWidth = 2;
      ctx.beginPath();
      for (let x = -45; x <= 45; x += 3) {
        let ty = 4.0 + 3.0 * Math.sin(x * 0.08) + 2.2 * Math.cos(x * 0.18) + 1.2 * Math.sin(x * 0.45);
        if (x >= -32 && x <= -22) ty = 5.0;
        else if (x >= -5 && x <= 5) ty = 8.5;
        else if (x >= 24 && x <= 29.5) ty = 14.0;
        const sx = toScreenX(x);
        const sy = toScreenY(ty);
        if (x === -45) ctx.moveTo(sx, sy);
        else ctx.lineTo(sx, sy);
      }
      ctx.stroke();

      // 3. Landing Extraction Pads
      const pads = [
        { name: 'ALPHA (1x)', x1: -32, x2: -22, y: 5.0, col: '#38bdf8' },
        { name: 'BETA (2x)', x1: -5, x2: 5, y: 8.5, col: '#facc15' },
        { name: 'GAMMA (3x)', x1: 24, x2: 29.5, y: 14.0, col: '#f472b6' }
      ];

      pads.forEach(pad => {
        const sx1 = toScreenX(pad.x1);
        const sx2 = toScreenX(pad.x2);
        const sy = toScreenY(pad.y);

        // Guide corridor
        ctx.save();
        ctx.strokeStyle = 'rgba(56, 189, 248, 0.25)';
        ctx.setLineDash([3, 3]);
        ctx.beginPath();
        ctx.moveTo(sx1, sy); ctx.lineTo(sx1, 15);
        ctx.moveTo(sx2, sy); ctx.lineTo(sx2, 15);
        ctx.stroke();
        ctx.restore();

        // Pad Deck
        ctx.save();
        ctx.strokeStyle = pad.col;
        ctx.lineWidth = 3.5;
        ctx.shadowColor = pad.col;
        ctx.shadowBlur = 8;
        ctx.beginPath();
        ctx.moveTo(sx1, sy); ctx.lineTo(sx2, sy);
        ctx.stroke();
        ctx.restore();

        // Pad Label
        ctx.fillStyle = pad.col;
        ctx.font = 'bold 8px sans-serif';
        ctx.textAlign = 'center';
        ctx.fillText(pad.name, (sx1 + sx2) / 2, sy + 11);
      });

      // 4. Helium-3 Canisters
      const canisters = Array.isArray(stats.canisters) ? stats.canisters : [
        { x: -18, y: 18, collected: false },
        { x: 12, y: 22, collected: false },
        { x: -36, y: 24, collected: false },
        { x: 34, y: 28, collected: false }
      ];

      canisters.forEach(c => {
        if (c.collected) return;
        const cx = toScreenX(c.x);
        const cy = toScreenY(c.y);
        ctx.save();
        ctx.fillStyle = '#00f0ff';
        ctx.shadowColor = '#00f0ff';
        ctx.shadowBlur = 8;
        ctx.beginPath();
        ctx.moveTo(cx, cy - 4);
        ctx.lineTo(cx + 3, cy);
        ctx.lineTo(cx, cy + 4);
        ctx.lineTo(cx - 3, cy);
        ctx.closePath();
        ctx.fill();
        ctx.restore();
      });

      // 5. Lander Module
      let landerX = 0;
      let landerY = 25;
      if (stats.landerPos && typeof stats.landerPos.x === 'number') {
        landerX = stats.landerPos.x;
        landerY = stats.landerPos.y;
      } else if (typeof stats.x === 'number') {
        landerX = stats.x;
        landerY = stats.y || 25;
      } else {
        landerX = normX * 30;
      }

      const lx = toScreenX(landerX);
      const ly = toScreenY(landerY);
      const rollAngleRad = (rollDeg * Math.PI) / 180;

      ctx.save();
      ctx.translate(lx, ly);
      ctx.rotate(rollAngleRad);

      // Main Thruster Plume Flame
      const isThrusting = stats.isThrusting !== false && stats.fuel > 0;
      if (isThrusting) {
        ctx.fillStyle = 'rgba(56, 189, 248, 0.85)';
        ctx.shadowColor = '#38bdf8';
        ctx.shadowBlur = 10;
        ctx.beginPath();
        ctx.moveTo(-3, 6);
        ctx.lineTo(0, 14 + Math.random() * 5);
        ctx.lineTo(3, 6);
        ctx.closePath();
        ctx.fill();
      }

      // RCS Plume puffs
      if (rcsBurstAnim.active) {
        ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
        ctx.beginPath();
        ctx.arc(-8, 0, 3, 0, Math.PI * 2);
        ctx.arc(8, 0, 3, 0, Math.PI * 2);
        ctx.fill();
      }

      // Landing Legs
      ctx.strokeStyle = '#94a3b8';
      ctx.lineWidth = 1.5;
      ctx.beginPath();
      ctx.moveTo(-5, 3); ctx.lineTo(-8, 8); ctx.lineTo(-10, 8);
      ctx.moveTo(5, 3); ctx.lineTo(8, 8); ctx.lineTo(10, 8);
      ctx.stroke();

      // Hull
      const hullGrad = ctx.createLinearGradient(-6, -5, 6, 5);
      hullGrad.addColorStop(0, '#facc15');
      hullGrad.addColorStop(1, '#ca8a04');
      ctx.fillStyle = hullGrad;
      ctx.beginPath();
      ctx.moveTo(-5, -6); ctx.lineTo(5, -6);
      ctx.lineTo(7, -2); ctx.lineTo(7, 3);
      ctx.lineTo(5, 5); ctx.lineTo(-5, 5);
      ctx.lineTo(-7, 3); ctx.lineTo(-7, -2);
      ctx.closePath();
      ctx.fill();

      // Cockpit
      ctx.fillStyle = '#0f172a';
      ctx.beginPath();
      ctx.arc(0, -1, 2.5, 0, Math.PI * 2);
      ctx.fill();

      ctx.restore();

      // 6. Velocity Vector Arrow
      const vx = typeof stats.horizontalSpeed === 'number' ? stats.horizontalSpeed : 0.0;
      const vy = typeof stats.verticalSpeed === 'number' ? stats.verticalSpeed : 0.0;
      const speedMag = Math.sqrt(vx * vx + vy * vy);
      if (speedMag > 0.15) {
        const arrowLen = Math.min(30, speedMag * 6.0);
        const dirX = (vx / speedMag) * arrowLen;
        const dirY = (-vy / speedMag) * arrowLen;

        ctx.save();
        ctx.strokeStyle = speedMag <= 1.8 ? '#34d399' : (speedMag <= 3.2 ? '#fbbf24' : '#ef4444');
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(lx, ly);
        ctx.lineTo(lx + dirX, ly + dirY);
        ctx.stroke();
        ctx.restore();
      }

      if (rcsBurstAnim.active) {
        rcsBurstAnim.timer -= 0.03;
        if (rcsBurstAnim.timer <= 0) rcsBurstAnim.active = false;
      }
    }

    function setLanderGravity(planet) {
      landerGravity = planet.toLowerCase();
      if (llGravMoon) llGravMoon.classList.toggle('active', landerGravity === 'moon');
      if (llGravMars) llGravMars.classList.toggle('active', landerGravity === 'mars');
      if (llGravEuropa) llGravEuropa.classList.toggle('active', landerGravity === 'europa');
      if (llGravEarth) llGravEarth.classList.toggle('active', landerGravity === 'earth');

      sendWsMessage({
        type: 'set_lander_gravity',
        planet: landerGravity,
        value: landerGravity
      });
    }

    function triggerEmergencyRcs() {
      rcsBurstAnim = { active: true, timer: 0.35 };
      sendWsMessage({ type: 'trigger_emergency_rcs' });
    }

    function refuelLander() {
      sendWsMessage({ type: 'refuel_lander' });
    }

