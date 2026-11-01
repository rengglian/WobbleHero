// Compass Labyrinth Cockpit

    // Compass Labyrinth Elements
    const viewCompassLabyrinth = document.getElementById('view-CompassLabyrinth');
    const clStateBadge = document.getElementById('clStateBadge');
    const clScore = document.getElementById('clScore');
    const clHeading = document.getElementById('clHeading');
    const clCardinalPlane = document.getElementById('clCardinalPlane');
    const clRelics = document.getElementById('clRelics');
    const clPlates = document.getElementById('clPlates');
    const clGravityVectorText = document.getElementById('clGravityVectorText');
    const clRateLabel = document.getElementById('clRateLabel');
    const clOffsetSlider = document.getElementById('clOffsetSlider');
    const clOffsetText = document.getElementById('clOffsetText');
    const clSeedInput = document.getElementById('clSeedInput');
    const clSensitivitySlider = document.getElementById('clSensitivitySlider');
    const clSensitivityText = document.getElementById('clSensitivityText');
    const clBtnInvertRoll = document.getElementById('clBtnInvertRoll');
    const clBtnInvertPitch = document.getElementById('clBtnInvertPitch');

    let compassOffsetDeg = 0;

    // Gravity Pinball Elements
    const viewGravityPinball = document.getElementById('view-GravityPinball');

    function renderCompassTelemetry(data, b, normX, normY, rollDeg, pitchDeg) {
      if (clStateBadge) clStateBadge.textContent = currentGameState;
      if (clScore) clScore.textContent = data.game ? (data.game.score || 0).toLocaleString() : 0;
      if (clRateLabel) clRateLabel.textContent = `${b.hz || 0} Hz`;

      const stats = (data.game && data.game.stats) ? data.game.stats : {};
      const heading = typeof stats.heading === 'number' ? stats.heading : (typeof stats.headingDeg === 'number' ? stats.headingDeg : (b.compassHeadingDeg || 0.0));
      const cardinal = stats.cardinalPlane || 'NORTH';
      const relics = stats.relicsCollected || 0;
      const totalRelics = stats.totalRelics || 3;
      const plates = stats.platesSolved || 0;
      const totalPlates = stats.totalPlates || 2;

      if (clHeading) clHeading.textContent = `${heading.toFixed(1)}°`;

      if (clCardinalPlane) {
        let arrow = '⬆️';
        if (cardinal === 'EAST') arrow = '➡️';
        else if (cardinal === 'SOUTH') arrow = '⬇️';
        else if (cardinal === 'WEST') arrow = '⬅️';
        clCardinalPlane.textContent = `${cardinal} ${arrow}`;
      }

      if (clRelics) clRelics.textContent = `${relics}/${totalRelics}`;
      if (clPlates) clPlates.textContent = `${plates}/${totalPlates}`;

      if (clGravityVectorText) {
        let vecStr = '(0, -1)';
        if (cardinal === 'EAST') vecStr = '(+1, 0)';
        else if (cardinal === 'SOUTH') vecStr = '(0, +1)';
        else if (cardinal === 'WEST') vecStr = '(-1, 0)';
        clGravityVectorText.textContent = `GRAVITY: ${vecStr}`;
      }

      if (typeof stats.seed === 'number' && document.activeElement !== clSeedInput) {
        clSeedInput.value = stats.seed;
      }

      if (typeof stats.compassOffset === 'number' && document.activeElement !== clOffsetSlider) {
        compassOffsetDeg = stats.compassOffset;
        clOffsetSlider.value = compassOffsetDeg;
        clOffsetText.textContent = `${Math.round(compassOffsetDeg)}°`;
      }

      drawCompassRadar(stats, normX, normY, heading);
    }

    function drawCompassRadar(stats, normX, normY, headingDeg) {
      const canvas = document.getElementById('clCompassCanvas');
      if (!canvas) return;
      const ctx = canvas.getContext('2d');
      const w = canvas.width;
      const h = canvas.height;

      ctx.clearRect(0, 0, w, h);

      // LAYER 1: ROTATING COMPASS ROSE (Left Side: cx = 72, cy = 115)
      const rcx = 72;
      const rcy = 115;
      const rRad = 52;
      const headingRad = (headingDeg * Math.PI) / 180;

      // Brass outer bezel ring
      ctx.save();
      const bezelGrad = ctx.createLinearGradient(rcx - rRad, rcy - rRad, rcx + rRad, rcy + rRad);
      bezelGrad.addColorStop(0, '#ca8a04');
      bezelGrad.addColorStop(0.5, '#facc15');
      bezelGrad.addColorStop(1, '#854d0e');
      ctx.strokeStyle = bezelGrad;
      ctx.lineWidth = 4;
      ctx.beginPath();
      ctx.arc(rcx, rcy, rRad, 0, Math.PI * 2);
      ctx.stroke();

      // Dial face fill
      ctx.fillStyle = '#0f172a';
      ctx.beginPath();
      ctx.arc(rcx, rcy, rRad - 2, 0, Math.PI * 2);
      ctx.fill();

      // Cardinal tick marks
      ctx.strokeStyle = 'rgba(255, 255, 255, 0.2)';
      ctx.lineWidth = 1;
      for (let a = 0; a < 360; a += 45) {
        const rad = (a * Math.PI) / 180;
        const inner = a % 90 === 0 ? rRad - 12 : rRad - 8;
        ctx.beginPath();
        ctx.moveTo(rcx + Math.sin(rad) * inner, rcy - Math.cos(rad) * inner);
        ctx.lineTo(rcx + Math.sin(rad) * (rRad - 4), rcy - Math.cos(rad) * (rRad - 4));
        ctx.stroke();
      }

      // Cardinal Labels (N, E, S, W)
      ctx.font = 'bold 9px sans-serif';
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.fillStyle = '#ef4444'; ctx.fillText('N', rcx, rcy - (rRad - 16));
      ctx.fillStyle = '#94a3b8'; ctx.fillText('S', rcx, rcy + (rRad - 16));
      ctx.fillStyle = '#94a3b8'; ctx.fillText('E', rcx + (rRad - 16), rcy);
      ctx.fillStyle = '#94a3b8'; ctx.fillText('W', rcx - (rRad - 16), rcy);

      // Rotating Magnetic Needle
      ctx.save();
      ctx.translate(rcx, rcy);
      ctx.rotate(-headingRad);

      // North Needle (Crimson)
      ctx.fillStyle = '#ef4444';
      ctx.shadowColor = '#ef4444';
      ctx.shadowBlur = 8;
      ctx.beginPath();
      ctx.moveTo(0, -(rRad - 14));
      ctx.lineTo(5, -4);
      ctx.lineTo(-5, -4);
      ctx.closePath();
      ctx.fill();

      // South Needle (Silver)
      ctx.shadowBlur = 0;
      ctx.fillStyle = '#cbd5e1';
      ctx.beginPath();
      ctx.moveTo(0, rRad - 14);
      ctx.lineTo(5, 4);
      ctx.lineTo(-5, 4);
      ctx.closePath();
      ctx.fill();

      // Center Pivot Pin
      ctx.fillStyle = '#facc15';
      ctx.beginPath();
      ctx.arc(0, 0, 4, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();
      ctx.restore();

      // Needle readout label
      ctx.font = 'bold 9px sans-serif';
      ctx.textAlign = 'center';
      ctx.fillStyle = '#facc15';
      ctx.fillText(`${headingDeg.toFixed(0)}°`, rcx, rcy + rRad + 14);

      // LAYER 2: OVERHEAD DUNGEON RADAR MINI-MAP (Right Side: 152..327)
      const dLeft = 152;
      const dTop = 18;
      const dSize = 175;

      // Dungeon boundary floor
      ctx.fillStyle = '#090d16';
      ctx.strokeStyle = '#334155';
      ctx.lineWidth = 2;
      ctx.fillRect(dLeft, dTop, dSize, dSize);
      ctx.strokeRect(dLeft, dTop, dSize, dSize);

      // Grid Cells (8x8)
      const cells = 8;
      const cellSize = dSize / cells;

      ctx.strokeStyle = 'rgba(255, 255, 255, 0.04)';
      ctx.lineWidth = 1;
      for (let i = 1; i < cells; i++) {
        ctx.beginPath();
        ctx.moveTo(dLeft + i * cellSize, dTop);
        ctx.lineTo(dLeft + i * cellSize, dTop + dSize);
        ctx.moveTo(dLeft, dTop + i * cellSize);
        ctx.lineTo(dLeft + dSize, dTop + i * cellSize);
        ctx.stroke();
      }

      // Walls
      const walls = [
        { x: 2, y: 1 }, { x: 2, y: 2 }, { x: 2, y: 3 },
        { x: 5, y: 4 }, { x: 5, y: 5 }, { x: 5, y: 6 }
      ];
      ctx.fillStyle = '#1e293b';
      walls.forEach(w => {
        ctx.fillRect(dLeft + w.x * cellSize + 1, dTop + w.y * cellSize + 1, cellSize - 2, cellSize - 2);
      });

      // Pressure Plates (Sunken purple pads)
      const plates = Array.isArray(stats.plates) ? stats.plates : [
        { x: 1, y: 6, active: false },
        { x: 6, y: 1, active: false }
      ];
      plates.forEach(p => {
        ctx.save();
        ctx.strokeStyle = p.active ? '#34d399' : '#a855f7';
        ctx.fillStyle = p.active ? 'rgba(52, 211, 153, 0.3)' : 'rgba(168, 85, 247, 0.2)';
        ctx.lineWidth = 2;
        const px = dLeft + p.x * cellSize + 3;
        const py = dTop + p.y * cellSize + 3;
        ctx.fillRect(px, py, cellSize - 6, cellSize - 6);
        ctx.strokeRect(px, py, cellSize - 6, cellSize - 6);
        ctx.restore();
      });

      // Ancient Relics (Golden artifacts)
      const relics = Array.isArray(stats.relics) ? stats.relics : [
        { x: 4, y: 2, collected: false },
        { x: 6, y: 6, collected: false }
      ];
      relics.forEach(r => {
        if (r.collected) return;
        const rx = dLeft + (typeof r.x === 'number' && Math.abs(r.x) > 8 ? (r.x + 24) / 48 * dSize : (r.x || 4) * cellSize + cellSize / 2);
        const ry = dTop + (typeof r.y === 'number' && Math.abs(r.y) > 8 ? (r.y + 24) / 48 * dSize : (r.y || 2) * cellSize + cellSize / 2);
        ctx.save();
        ctx.fillStyle = '#facc15';
        ctx.shadowColor = '#facc15';
        ctx.shadowBlur = 8;
        ctx.beginPath();
        ctx.arc(rx, ry, 4, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
      });

      // Sliding Monolith Blocks (Amber cubes)
      const blocks = Array.isArray(stats.blocks) ? stats.blocks : [
        { x: 1, y: 2 },
        { x: 6, y: 4 }
      ];
      blocks.forEach(b => {
        const bx = dLeft + (typeof b.x === 'number' && Math.abs(b.x) > 8 ? (b.x + 24) / 48 * dSize : (b.x || 1) * cellSize + 2);
        const by = dTop + (typeof b.y === 'number' && Math.abs(b.y) > 8 ? (b.y + 24) / 48 * dSize : (b.y || 2) * cellSize + 2);
        ctx.save();
        ctx.fillStyle = '#f59e0b';
        ctx.shadowColor = '#f59e0b';
        ctx.shadowBlur = 6;
        ctx.fillRect(bx, by, cellSize - 4, cellSize - 4);
        ctx.strokeStyle = '#d97706';
        ctx.lineWidth = 1.5;
        ctx.strokeRect(bx, by, cellSize - 4, cellSize - 4);
        ctx.restore();
      });

      // Explorer Hero (Cyan marker with roll/pitch tilt vector)
      let ex = dLeft + 3.5 * cellSize;
      let ey = dTop + 4.5 * cellSize;
      if (stats.playerPos && typeof stats.playerPos.x === 'number') {
        ex = dLeft + (Math.abs(stats.playerPos.x) > 8 ? (stats.playerPos.x + 24) / 48 * dSize : (stats.playerPos.x / 8) * dSize);
        ey = dTop + (Math.abs(stats.playerPos.y) > 8 ? (stats.playerPos.y + 24) / 48 * dSize : (stats.playerPos.y / 8) * dSize);
      } else {
        ex += normX * 12;
        ey += normY * 12;
      }

      ctx.save();
      ctx.fillStyle = '#38bdf8';
      ctx.shadowColor = '#00f0ff';
      ctx.shadowBlur = 10;
      ctx.beginPath();
      ctx.arc(ex, ey, 5.5, 0, Math.PI * 2);
      ctx.fill();
      ctx.fillStyle = '#ffffff';
      ctx.beginPath();
      ctx.arc(ex, ey, 2, 0, Math.PI * 2);
      ctx.fill();
      ctx.restore();

      // Current Gravity Slide Vector Arrow
      const cardinal = stats.cardinalPlane || 'NORTH';
      let gdx = 0, gdy = -1;
      if (cardinal === 'EAST') { gdx = 1; gdy = 0; }
      else if (cardinal === 'SOUTH') { gdx = 0; gdy = 1; }
      else if (cardinal === 'WEST') { gdx = -1; gdy = 0; }

      ctx.save();
      ctx.strokeStyle = '#a78bfa';
      ctx.fillStyle = '#a78bfa';
      ctx.lineWidth = 2;
      const gcx = dLeft + dSize - 16;
      const gcy = dTop + 16;
      ctx.beginPath();
      ctx.moveTo(gcx, gcy);
      ctx.lineTo(gcx + gdx * 10, gcy + gdy * 10);
      ctx.stroke();
      ctx.restore();
    }

    function onCompassOffsetInput(val) {
      compassOffsetDeg = parseFloat(val);
      if (clOffsetText) clOffsetText.textContent = `${Math.round(compassOffsetDeg)}°`;
      sendWsMessage({
        type: 'set_compass_offset',
        offset: compassOffsetDeg,
        value: compassOffsetDeg
      });
    }

    function rotateCompass(deltaDeg) {
      sendWsMessage({
        type: 'rotate_compass',
        delta: deltaDeg,
        deltaDeg: deltaDeg,
        value: deltaDeg
      });
    }

    function applyCompassSeed() {
      const val = parseInt(clSeedInput.value, 10);
      if (!isNaN(val)) {
        sendWsMessage({ type: 'set_compass_seed', seed: val });
      }
    }

    function randomizeCompassSeed() {
      const randSeed = Math.floor(Math.random() * 90000) + 1000;
      clSeedInput.value = randSeed;
      sendWsMessage({ type: 'set_compass_seed', seed: randSeed });
    }

