// WobbleHero Companion App Core
    let ws = null;
    let currentGameState = 'START';
    let activeGameId = '';
    let lastPacketTime = 0;
    let isRecordingActive = false;

    // Shell Elements
    const wsDot = document.getElementById('wsDot');
    const wsText = document.getElementById('wsText');
    const gameGrid = document.getElementById('gameGrid');
    const activeGameBadge = document.getElementById('activeGameBadge');
    const mainBtn = document.getElementById('mainBtn');
    const mainBtnIcon = document.getElementById('mainBtnIcon');
    const mainBtnText = document.getElementById('mainBtnText');

    // Balance Trainer Elements
    const viewBalanceTrainer = document.getElementById('view-BalanceTrainer');
    const btStateBadge = document.getElementById('btStateBadge');
    const btScore = document.getElementById('btScore');
    const btMultiplier = document.getElementById('btMultiplier');
    const btTime = document.getElementById('btTime');
    const btFalls = document.getElementById('btFalls');
    const btRateLabel = document.getElementById('btRateLabel');
    const btBubblePuck = document.getElementById('btBubblePuck');
    const btRollDeg = document.getElementById('btRollDeg');
    const btPitchDeg = document.getElementById('btPitchDeg');
    const btSensitivitySlider = document.getElementById('btSensitivitySlider');
    const btSensitivityText = document.getElementById('btSensitivityText');
    const btBtnInvertRoll = document.getElementById('btBtnInvertRoll');
    const btBtnInvertPitch = document.getElementById('btBtnInvertPitch');

    // Wobble Runner Elements
    const viewWobbleRunner = document.getElementById('view-WobbleRunner');
    const wrSeedBadge = document.getElementById('wrSeedBadge');
    const wrScore = document.getElementById('wrScore');
    const wrDistance = document.getElementById('wrDistance');
    const wrSpeed = document.getElementById('wrSpeed');
    const wrMultiplier = document.getElementById('wrMultiplier');
    const wrApples = document.getElementById('wrApples');
    const wrHorseshoes = document.getElementById('wrHorseshoes');
    const wrSteerDeg = document.getElementById('wrSteerDeg');
    const wrRiderPuck = document.getElementById('wrRiderPuck');
    const wrSensitivitySlider = document.getElementById('wrSensitivitySlider');
    const wrSensitivityText = document.getElementById('wrSensitivityText');
    const wrBtnInvertRoll = document.getElementById('wrBtnInvertRoll');
    const wrPlayerNameInput = document.getElementById('wrPlayerNameInput');
    const wrSeedInput = document.getElementById('wrSeedInput');
    const leaderboardBody = document.getElementById('leaderboardBody');

    // Wobble Maze Elements
    const viewWobbleMaze = document.getElementById('view-WobbleMaze');
    const wmStateBadge = document.getElementById('wmStateBadge');
    const wmTime = document.getElementById('wmTime');
    const wmGems = document.getElementById('wmGems');
    const wmKey = document.getElementById('wmKey');
    const wmFalls = document.getElementById('wmFalls');
    const wmConfigBadge = document.getElementById('wmConfigBadge');
    const wmSeedInput = document.getElementById('wmSeedInput');
    const wmSizeSmall = document.getElementById('wmSizeSmall');
    const wmSizeMedium = document.getElementById('wmSizeMedium');
    const wmSizeLarge = document.getElementById('wmSizeLarge');
    const wmDiffEasy = document.getElementById('wmDiffEasy');
    const wmDiffMedium = document.getElementById('wmDiffMedium');
    const wmDiffHard = document.getElementById('wmDiffHard');
    const wmRateLabel = document.getElementById('wmRateLabel');
    const wmBubblePuck = document.getElementById('wmBubblePuck');
    const wmRollDeg = document.getElementById('wmRollDeg');
    const wmPitchDeg = document.getElementById('wmPitchDeg');
    const wmSensitivitySlider = document.getElementById('wmSensitivitySlider');
    const wmSensitivityText = document.getElementById('wmSensitivityText');
    const wmBtnInvertRoll = document.getElementById('wmBtnInvertRoll');
    const wmBtnInvertPitch = document.getElementById('wmBtnInvertPitch');

    let mazeSize = 'medium';
    let mazeDifficulty = 'medium';

    // Asteroid Surfer Elements
    // Sensor Lab Elements
    const viewSensorLab = document.getElementById('view-SensorLab');
    const slRecHzBadge = document.getElementById('slRecHzBadge');
    const btnRecordToggle = document.getElementById('btnRecordToggle');
    const btnRecordIcon = document.getElementById('btnRecordIcon');
    const btnRecordText = document.getElementById('btnRecordText');
    const recordingStatsRow = document.getElementById('recordingStatsRow');
    const recDurationText = document.getElementById('recDurationText');
    const recSampleText = document.getElementById('recSampleText');
    const downloadRow = document.getElementById('downloadRow');
    const slSourceBadge = document.getElementById('slSourceBadge');
    const slBubblePuck = document.getElementById('slBubblePuck');
    const slRollDeg = document.getElementById('slRollDeg');
    const slPitchDeg = document.getElementById('slPitchDeg');
    const slDeviceIpLabel = document.getElementById('slDeviceIpLabel');
    const slHeadingVal = document.getElementById('slHeadingVal');
    const slMagVal = document.getElementById('slMagVal');
    const slAccelVal = document.getElementById('slAccelVal');
    const slGyroVal = document.getElementById('slGyroVal');
    const slHzVal = document.getElementById('slHzVal');
    const slSourceVal = document.getElementById('slSourceVal');
    const slBtnMountUnder = document.getElementById('slBtnMountUnder');
    const slBtnMountTop = document.getElementById('slBtnMountTop');
    const slBtnInvertRoll = document.getElementById('slBtnInvertRoll');
    const slBtnInvertPitch = document.getElementById('slBtnInvertPitch');

    // Map of Views
    const views = {
      'BalanceTrainer': viewBalanceTrainer,
      'WobbleRunner': viewWobbleRunner,
      'WobbleMaze': viewWobbleMaze,
      'SensorLab': viewSensorLab,
      'AsteroidSurfer': viewAsteroidSurfer,
      'WobbleSumo': viewWobbleSumo,
      'LunarLander': viewLunarLander,
      'CompassLabyrinth': viewCompassLabyrinth,
      'GravityPinball': viewGravityPinball,
      'DownhillSlalom': viewDownhillSlalom,
      'RiverLogRoll': viewRiverLogRoll
    };


const GAME_VIEWS = [
  { id: 'BalanceTrainer', file: '/views/balance_trainer.html' },
  { id: 'WobbleRunner', file: '/views/wobble_runner.html' },
  { id: 'WobbleMaze', file: '/views/wobble_maze.html' },
  { id: 'AsteroidSurfer', file: '/views/asteroid_surfer.html' },
  { id: 'WobbleSumo', file: '/views/wobble_sumo.html' },
  { id: 'LunarLander', file: '/views/lunar_lander.html' },
  { id: 'CompassLabyrinth', file: '/views/compass_labyrinth.html' },
  { id: 'GravityPinball', file: '/views/gravity_pinball.html' },
  { id: 'DownhillSlalom', file: '/views/downhill_slalom.html' },
  { id: 'RiverLogRoll', file: '/views/river_log_roll.html' },
  { id: 'SensorLab', file: '/views/sensor_lab.html' }
];

async function loadAllViews() {
  const container = document.getElementById('views-container');
  if (!container) return;
  container.innerHTML = '';
  
  await Promise.all(GAME_VIEWS.map(async (v) => {
    try {
      const res = await fetch(v.file);
      if (res.ok) {
        const html = await res.text();
        container.insertAdjacentHTML('beforeend', html);
      }
    } catch (e) {
      console.error("Failed to load view " + v.file, e);
    }
  }));

  // Re-populate views map now that all view elements are in the DOM
  GAME_VIEWS.forEach(v => {
    views[v.id] = document.getElementById("view-" + v.id);
  });
}
    function connectWs() {
      const proto = window.location.protocol === 'https:' ? 'wss:' : 'ws:';
      const wsUrl = `${proto}//${window.location.host}/ws`;
      ws = new WebSocket(wsUrl);

      ws.onopen = () => {
        wsDot.className = 'dot dot-online';
        wsText.textContent = 'Connected';
      };

      ws.onmessage = (event) => {
        try {
          const data = JSON.parse(event.data);
          if (data.type === 'telemetry') {
            onTelemetry(data);
          }
        } catch (e) {
          console.error('WS Parse Error', e);
        }
      };

      ws.onclose = () => {
        wsDot.className = 'dot dot-offline';
        wsText.textContent = 'Disconnected';
        setTimeout(connectWs, 1500);
      };

      ws.onerror = () => {
        ws.close();
      };
    }

    function sendWsMessage(payload) {
      if (ws && ws.readyState === WebSocket.OPEN) {
        ws.send(JSON.stringify(payload));
      }
    }

    function switchActiveGameView(targetGameId) {
      if (!targetGameId) return;
      activeGameId = targetGameId;

      // Show only active game view
      Object.keys(views).forEach(key => {
        if (views[key]) {
          views[key].classList.toggle('active', key.toLowerCase() === targetGameId.toLowerCase());
        }
      });

      // Update game switcher tabs active state
      Array.from(gameGrid.children).forEach(card => {
        const id = card.getAttribute('data-id');
        card.classList.toggle('active', id && id.toLowerCase() === targetGameId.toLowerCase());
      });
    }

    function onTelemetry(data) {
      lastPacketTime = Date.now();

      // 1. Determine active game and switch view if changed
      if (data.game) {
        const g = data.game;
        const gId = g.activeGameId || '';
        if (gId && gId !== activeGameId) {
          switchActiveGameView(gId);
        }
        activeGameBadge.textContent = g.activeGameTitle || gId;
        currentGameState = g.state || 'START';
        updateMainBtnState(currentGameState);
      }

      // 2. Render available games in switcher if not populated or changed
      if (Array.isArray(data.availableGames)) {
        renderGames(data.availableGames, activeGameId);
      }

      const b = data.board || {};
      const rollDeg = typeof b.fusedRollDeg === 'number' ? b.fusedRollDeg : 0;
      const pitchDeg = typeof b.fusedPitchDeg === 'number' ? b.fusedPitchDeg : 0;
      const signR = rollDeg >= 0 ? '+' : '';
      const signP = pitchDeg >= 0 ? '+' : '';

      // 2D Puck coordinates (-1..1 to 10%..90%)
      const normX = Math.max(-1, Math.min(1, b.roll || 0));
      const normY = Math.max(-1, Math.min(1, b.pitch || 0));
      const leftPct = 50 + (normX * 40);
      const topPct = 50 + (normY * 40);

      // 3. Update view-specific content based on active game
      const current = (activeGameId || '').toLowerCase();

      if (current === 'balancetrainer') {
        // Balance Trainer
        btStateBadge.textContent = currentGameState;
        btScore.textContent = data.game ? (data.game.score || 0) : 0;

        if (data.game && data.game.stats) {
          const stats = data.game.stats;
          btMultiplier.textContent = `${(stats.multiplier || 1.0).toFixed(1)}x`;
          btTime.textContent = `${Math.ceil(stats.timeRemaining || 60)}s`;
          btFalls.textContent = stats.falls || 0;
        }

        btRateLabel.textContent = `${b.hz || 0} Hz`;
        btRollDeg.textContent = `${signR}${rollDeg.toFixed(1)}°`;
        btPitchDeg.textContent = `${signP}${pitchDeg.toFixed(1)}°`;
        btBubblePuck.style.left = `${leftPct}%`;
        btBubblePuck.style.top = `${topPct}%`;
      }
      else if (current === 'wobblerunner') {
        // Wobble Runner
        wrScore.textContent = data.game ? (data.game.score || 0).toLocaleString() : 0;

        if (data.game && data.game.stats) {
          const stats = data.game.stats;
          wrDistance.textContent = `${(stats.distance || 0).toLocaleString()}m`;
          wrSpeed.textContent = `${stats.speed || 0} km/h`;
          wrMultiplier.textContent = `${stats.multiplier || 1}x`;
          wrApples.textContent = stats.apples || 0;
          wrHorseshoes.textContent = stats.horseshoes || 0;

          if (typeof stats.seed === 'number') {
            wrSeedBadge.textContent = `Seed: ${stats.seed}`;
            if (document.activeElement !== wrSeedInput) {
              wrSeedInput.value = stats.seed;
            }
          }
        }

        // Steer indicator puck (lateral position from roll)
        wrSteerDeg.textContent = `${signR}${rollDeg.toFixed(1)}°`;
        const steerPct = 50 + (normX * 40);
        wrRiderPuck.style.left = `${Math.max(10, Math.min(90, steerPct))}%`;
      }
      else if (current === 'wobblemaze') {
        // Wobble Maze
        wmStateBadge.textContent = currentGameState;

        if (data.game && data.game.stats) {
          const stats = data.game.stats;
          const timeElapsed = typeof stats.timeElapsed === 'number' ? stats.timeElapsed : (data.game.score || 0);
          wmTime.textContent = `${timeElapsed.toFixed(1)}s`;

          const gems = typeof stats.gemsCollected === 'number' ? stats.gemsCollected : 0;
          const totalGems = typeof stats.totalGems === 'number' ? stats.totalGems : 3;
          wmGems.textContent = `${gems}/${totalGems}`;

          if (stats.gateUnlocked) {
            wmKey.innerHTML = '<span style="color:#34d399; font-weight:800;">🔓 OPEN</span>';
          } else if (stats.hasKey) {
            wmKey.innerHTML = '<span style="color:#fbbf24; font-weight:800;">🗝️ YES</span>';
          } else {
            wmKey.innerHTML = '<span style="color:#94a3b8;">NO</span>';
          }

          wmFalls.textContent = stats.falls || 0;

          if (typeof stats.seed === 'number') {
            wmConfigBadge.textContent = `Seed: ${stats.seed}`;
            if (document.activeElement !== wmSeedInput) {
              wmSeedInput.value = stats.seed;
            }
          }

          if (stats.size && typeof stats.size === 'string') {
            const s = stats.size.toLowerCase();
            mazeSize = s;
            wmSizeSmall.classList.toggle('active', s === 'small');
            wmSizeMedium.classList.toggle('active', s === 'medium');
            wmSizeLarge.classList.toggle('active', s === 'large');
          }

          if (stats.difficulty && typeof stats.difficulty === 'string') {
            const d = stats.difficulty.toLowerCase();
            mazeDifficulty = d;
            wmDiffEasy.classList.toggle('active', d === 'easy');
            wmDiffMedium.classList.toggle('active', d === 'medium');
            wmDiffHard.classList.toggle('active', d === 'hard');
          }
        }

        wmRateLabel.textContent = `${b.hz || 0} Hz`;
        wmRollDeg.textContent = `${signR}${rollDeg.toFixed(1)}°`;
        wmPitchDeg.textContent = `${signP}${pitchDeg.toFixed(1)}°`;
        wmBubblePuck.style.left = `${leftPct}%`;
        wmBubblePuck.style.top = `${topPct}%`;
      }
      else if (current === 'asteroidsurfer') {
        // Asteroid Surfer Telemetry
        if (asStateBadge) asStateBadge.textContent = currentGameState;
        if (asScore) asScore.textContent = data.game ? (data.game.score || 0).toLocaleString() : 0;

        const stats = (data.game && data.game.stats) ? data.game.stats : {};
        const spd = typeof stats.speed === 'number' ? stats.speed : 0;
        const mult = typeof stats.multiplier === 'number' ? stats.multiplier : 1.0;
        const air = typeof stats.airTime === 'number' ? stats.airTime : 0.0;
        const dust = typeof stats.plasmaRings === 'number' ? stats.plasmaRings : (stats.cosmicDust || 0);
        const alt = typeof stats.lipHeight === 'number' ? stats.lipHeight : (stats.altitude || 0.0);

        if (asSpeed) asSpeed.textContent = `${Math.round(spd)} km/h`;
        if (asMultiplier) asMultiplier.textContent = `${mult.toFixed(1)}x`;
        if (asAirTime) asAirTime.textContent = `${air.toFixed(1)}s`;
        if (asCosmicDust) asCosmicDust.textContent = dust.toLocaleString();
        if (asLipHeight) asLipHeight.textContent = `${alt.toFixed(1)}m`;
        if (asRateLabel) asRateLabel.textContent = `${b.hz || 0} Hz`;

        if (asWaveStatusBadge) {
          if (stats.isAirborne || air > 0) {
            asWaveStatusBadge.textContent = '🚀 AIR-TIME';
            asWaveStatusBadge.style.color = '#f472b6';
          } else if (stats.isCarvingLip) {
            asWaveStatusBadge.textContent = '⚡ LIP CARVE';
            asWaveStatusBadge.style.color = '#facc15';
          } else {
            asWaveStatusBadge.textContent = '🌊 ON WAVE';
            asWaveStatusBadge.style.color = '#38bdf8';
          }
        }

        if (asBankDeg) asBankDeg.textContent = `${signR}${rollDeg.toFixed(1)}°`;
        if (asAltitudeVal) asAltitudeVal.textContent = `${alt.toFixed(1)}m`;

        // Sync wave speed slider if not actively dragging
        if (typeof stats.waveSpeed === 'number' && document.activeElement !== asWaveSpeedSlider) {
          currentWaveSpeed = stats.waveSpeed;
          if (asWaveSpeedSlider) asWaveSpeedSlider.value = stats.waveSpeed;
          if (asWaveSpeedText) asWaveSpeedText.textContent = `${Math.round(stats.waveSpeed)} km/h`;
        }

        // Sync cosmic density buttons
        if (stats.cosmicDensity && typeof stats.cosmicDensity === 'string') {
          const d = stats.cosmicDensity.toLowerCase();
          currentCosmicDensity = d;
          if (asDensityLow) asDensityLow.classList.toggle('active', d === 'low');
          if (asDensityMedium) asDensityMedium.classList.toggle('active', d === 'medium');
          if (asDensityHigh) asDensityHigh.classList.toggle('active', d === 'high');
        }

        // Render 2D Surf Carve Visualizer
        drawSurfVisualizer(normX, normY, rollDeg, pitchDeg, stats);
      }
      else if (current === 'wobblesumo') {
        renderSumoTelemetry(data, b, normX, normY, rollDeg, pitchDeg);
      }
      else if (current === 'lunarlander') {
        renderLanderTelemetry(data, b, normX, normY, rollDeg, pitchDeg);
      }
      else if (current === 'compasslabyrinth') {
        renderCompassTelemetry(data, b, normX, normY, rollDeg, pitchDeg);
      }
      else if (current === 'gravitypinball') {
        renderPinballTelemetry(data, b, normX, normY, rollDeg, pitchDeg);
      }
      else if (current === 'downhillslalom') {
        renderSlalomTelemetry(data, b, normX, normY, rollDeg, pitchDeg);
      }
      else if (current === 'riverlogroll') {
        renderLogRollTelemetry(data, b, normX, normY, rollDeg, pitchDeg);
      }
      else if (current === 'sensorlab') {
        // Sensor Lab
        slSourceBadge.textContent = `${(b.source || 'atoms3').toUpperCase()}`;
        slRollDeg.textContent = `${signR}${rollDeg.toFixed(1)}°`;
        slPitchDeg.textContent = `${signP}${pitchDeg.toFixed(1)}°`;
        slBubblePuck.style.left = `${leftPct}%`;
        slBubblePuck.style.top = `${topPct}%`;

        if (b.deviceIp) slDeviceIpLabel.textContent = `IP: ${b.deviceIp}`;
        slHzVal.textContent = `${b.hz || 0} Hz (${b.isActive ? 'Active' : 'Idle'})`;
        slSourceVal.textContent = `${(b.source || 'atoms3').toUpperCase()} (Seq: ${b.seq || 0})`;

        // Magnetometer & Compass
        if (b.hasMag) {
          slHeadingVal.textContent = `${(b.compassHeadingDeg || 0).toFixed(1)}°`;
          if (b.rawMag) {
            slMagVal.textContent = `X: ${b.rawMag.x.toFixed(1)}, Y: ${b.rawMag.y.toFixed(1)}, Z: ${b.rawMag.z.toFixed(1)}`;
          }
        } else {
          slHeadingVal.textContent = 'Compass Not Available';
          slMagVal.textContent = 'No Magnetometer Detected';
        }

        // Accel & Gyro
        if (b.rawAccel) {
          slAccelVal.textContent = `X: ${b.rawAccel.x.toFixed(2)}, Y: ${b.rawAccel.y.toFixed(2)}, Z: ${b.rawAccel.z.toFixed(2)}`;
        }
        if (b.rawGyro) {
          slGyroVal.textContent = `X: ${b.rawGyro.x.toFixed(1)}, Y: ${b.rawGyro.y.toFixed(1)}, Z: ${b.rawGyro.z.toFixed(1)}`;
        }

        // Recording stats
        if (data.game && data.game.stats) {
          const stats = data.game.stats;
          if (typeof stats.isRecording === 'boolean') {
            isRecordingActive = stats.isRecording;
            if (isRecordingActive) {
              btnRecordToggle.className = 'btn-main btn-record recording';
              btnRecordIcon.textContent = '⏹️';
              btnRecordText.textContent = 'Stop Recording Session';
              recordingStatsRow.style.display = 'flex';
              recDurationText.textContent = `${(stats.recordDuration || 0).toFixed(1)}s`;
              recSampleText.textContent = stats.sampleCount || 0;
              downloadRow.style.display = 'none';
            } else {
              btnRecordToggle.className = 'btn-main btn-record';
              btnRecordIcon.textContent = '⏺️';
              btnRecordText.textContent = 'Start Recording Session';
              if (stats.hasRecording) {
                recordingStatsRow.style.display = 'flex';
                recDurationText.textContent = `${(stats.recordDuration || 0).toFixed(1)}s`;
                recSampleText.textContent = stats.sampleCount || 0;
                downloadRow.style.display = 'block';
              }
            }
          }
        }
      }

      // 4. Global Settings Sync (Sliders, Invert toggles, Player Name)
      if (data.settings) {
        const s = data.settings;
        if (typeof s.sensitivity === 'number') {
          const sensText = `${s.sensitivity.toFixed(1)}x`;
          if (btSensitivitySlider) btSensitivitySlider.value = s.sensitivity;
          if (btSensitivityText) btSensitivityText.textContent = sensText;
          if (wrSensitivitySlider) wrSensitivitySlider.value = s.sensitivity;
          if (wrSensitivityText) wrSensitivityText.textContent = sensText;
          if (wmSensitivitySlider) wmSensitivitySlider.value = s.sensitivity;
          if (wmSensitivityText) wmSensitivityText.textContent = sensText;
          if (asSensitivitySlider) asSensitivitySlider.value = s.sensitivity;
          if (asSensitivityText) asSensitivityText.textContent = sensText;
          if (sumoSensitivitySlider) sumoSensitivitySlider.value = s.sensitivity;
          if (sumoSensitivityText) sumoSensitivityText.textContent = sensText;
          if (llSensitivitySlider) llSensitivitySlider.value = s.sensitivity;
          if (llSensitivityText) llSensitivityText.textContent = sensText;
          if (clSensitivitySlider) clSensitivitySlider.value = s.sensitivity;
          if (clSensitivityText) clSensitivityText.textContent = sensText;
          if (gpSensitivitySlider) gpSensitivitySlider.value = s.sensitivity;
          if (gpSensitivityText) gpSensitivityText.textContent = sensText;
          if (dsSensitivitySlider) dsSensitivitySlider.value = s.sensitivity;
          if (dsSensitivityText) dsSensitivityText.textContent = sensText;
          if (rlSensitivitySlider) rlSensitivitySlider.value = s.sensitivity;
          if (rlSensitivityText) rlSensitivityText.textContent = sensText;
          if (sumoBtnInvertRoll) sumoBtnInvertRoll.classList.toggle('active', invRoll);
          if (sumoBtnInvertPitch) sumoBtnInvertPitch.classList.toggle('active', invPitch);
          if (llBtnInvertRoll) llBtnInvertRoll.classList.toggle('active', invRoll);
          if (llBtnInvertPitch) llBtnInvertPitch.classList.toggle('active', invPitch);
          if (clBtnInvertRoll) clBtnInvertRoll.classList.toggle('active', invRoll);
          if (clBtnInvertPitch) clBtnInvertPitch.classList.toggle('active', invPitch);
          if (gpBtnInvertRoll) gpBtnInvertRoll.classList.toggle('active', invRoll);
          if (gpBtnInvertPitch) gpBtnInvertPitch.classList.toggle('active', invPitch);
          if (dsBtnInvertRoll) dsBtnInvertRoll.classList.toggle('active', invRoll);
          if (dsBtnInvertPitch) dsBtnInvertPitch.classList.toggle('active', invPitch);
          if (rlBtnInvertRoll) rlBtnInvertRoll.classList.toggle('active', invRoll);
          if (rlBtnInvertPitch) rlBtnInvertPitch.classList.toggle('active', invPitch);
        }

        const invRoll = !!s.invertRoll;
        const invPitch = !!s.invertPitch;
        if (btBtnInvertRoll) btBtnInvertRoll.classList.toggle('active', invRoll);
        if (btBtnInvertPitch) btBtnInvertPitch.classList.toggle('active', invPitch);
        if (wrBtnInvertRoll) wrBtnInvertRoll.classList.toggle('active', invRoll);
        if (wmBtnInvertRoll) wmBtnInvertRoll.classList.toggle('active', invRoll);
        if (wmBtnInvertPitch) wmBtnInvertPitch.classList.toggle('active', invPitch);
        if (asBtnInvertRoll) asBtnInvertRoll.classList.toggle('active', invRoll);
        if (asBtnInvertPitch) asBtnInvertPitch.classList.toggle('active', invPitch);
        if (slBtnInvertRoll) slBtnInvertRoll.classList.toggle('active', invRoll);
        if (slBtnInvertPitch) slBtnInvertPitch.classList.toggle('active', invPitch);

        if (slBtnMountUnder && slBtnMountTop) {
          const isUnder = invRoll && invPitch;
          const isTop = !invRoll && !invPitch;
          slBtnMountUnder.classList.toggle('active', isUnder);
          slBtnMountTop.classList.toggle('active', isTop);
        }

        if (s.playerName && document.activeElement !== wrPlayerNameInput) {
          wrPlayerNameInput.value = s.playerName;
        }
      }

      // 5. Leaderboard Sync
      if (Array.isArray(data.leaderboard)) {
        renderLeaderboard(data.leaderboard);
      }
    }

    function renderGames(games, activeId) {
      if (gameGrid.children.length === games.length) {
        Array.from(gameGrid.children).forEach((child, idx) => {
          child.classList.toggle('active', games[idx].id.toLowerCase() === (activeId || '').toLowerCase());
        });
        return;
      }

      gameGrid.innerHTML = '';
      games.forEach(g => {
        const card = document.createElement('div');
        const isActive = g.id.toLowerCase() === (activeId || '').toLowerCase();
        card.className = `game-card ${isActive ? 'active' : ''}`;
        card.setAttribute('data-id', g.id);
        card.innerHTML = `
          <div class="game-icon">${g.icon}</div>
          <div class="game-title">${g.title}</div>
        `;
        card.onclick = () => {
          sendWsMessage({ type: 'switch_game', gameId: g.id });
          switchActiveGameView(g.id);
        };
        gameGrid.appendChild(card);
      });
    }

    function updateMainBtnState(state) {
      if (state === 'RUNNING') {
        mainBtn.className = 'btn-main btn-pause';
        mainBtnIcon.textContent = '⏸️';
        mainBtnText.textContent = 'Pause Game';
      } else if (state === 'PAUSED') {
        mainBtn.className = 'btn-main btn-resume';
        mainBtnIcon.textContent = '▶️';
        mainBtnText.textContent = 'Resume Game';
      } else {
        mainBtn.className = 'btn-main btn-start';
        mainBtnIcon.textContent = '▶️';
        mainBtnText.textContent = 'Start Game';
      }
    }

    function onMainBtnClick() {
      if (currentGameState === 'RUNNING') {
        sendWsMessage({ type: 'pause_game' });
      } else if (currentGameState === 'PAUSED') {
        sendWsMessage({ type: 'resume_game' });
      } else {
        sendWsMessage({ type: 'start_game' });
      }
    }

    function onSensitivityInput(val) {
      const num = parseFloat(val);
      const text = `${num.toFixed(1)}x`;
      if (btSensitivityText) btSensitivityText.textContent = text;
      if (wrSensitivityText) wrSensitivityText.textContent = text;
      if (wmSensitivityText) wmSensitivityText.textContent = text;
      if (asSensitivityText) asSensitivityText.textContent = text;
      if (sumoSensitivityText) sumoSensitivityText.textContent = text;
      if (llSensitivityText) llSensitivityText.textContent = text;
      sendWsMessage({ type: 'set_sensitivity', value: num });
    }

    function toggleInvert(axis) {
      sendWsMessage({ type: 'toggle_invert', axis: axis });
    }

    function setMountingProfile(profile) {
      sendWsMessage({ type: 'set_mounting_profile', profile: profile });
    }

    function toggleRecordingSession() {
      if (isRecordingActive) {
        sendWsMessage({ type: 'stop_recording' });
      } else {
        sendWsMessage({ type: 'start_recording' });
      }
    }

    function savePlayerName() {
      const val = wrPlayerNameInput.value.trim();
      if (val) {
        sendWsMessage({ type: 'set_player_name', playerName: val });
      }
    }

    function applySeed() {
      const val = parseInt(wrSeedInput.value, 10);
      if (!isNaN(val)) {
        sendWsMessage({ type: 'set_seed', seed: val });
      }
    }

    function randomizeSeed() {
      const randomSeed = Math.floor(Math.random() * 900000) + 1000;
      wrSeedInput.value = randomSeed;
      sendWsMessage({ type: 'set_seed', seed: randomSeed });
    }

    function setMazeSize(size) {
      mazeSize = size.toLowerCase();
      wmSizeSmall.classList.toggle('active', mazeSize === 'small');
      wmSizeMedium.classList.toggle('active', mazeSize === 'medium');
      wmSizeLarge.classList.toggle('active', mazeSize === 'large');
      sendMazeConfig();
    }

    function setMazeDifficulty(diff) {
      mazeDifficulty = diff.toLowerCase();
      wmDiffEasy.classList.toggle('active', mazeDifficulty === 'easy');
      wmDiffMedium.classList.toggle('active', mazeDifficulty === 'medium');
      wmDiffHard.classList.toggle('active', mazeDifficulty === 'hard');
      sendMazeConfig();
    }

    function applyMazeConfig() {
      sendMazeConfig();
    }

    function randomizeMazeSeed() {
      const randomSeed = Math.floor(Math.random() * 900000) + 1000;
      wmSeedInput.value = randomSeed;
      sendMazeConfig();
    }

    function sendMazeConfig() {
      const seed = parseInt(wmSeedInput.value, 10) || 1337;
      sendWsMessage({
        type: 'set_maze_config',
        seed: seed,
        size: mazeSize,
        difficulty: mazeDifficulty
      });
    }

    function resetLeaderboard() {
      if (confirm("Reset the persistent top-10 leaderboard?")) {
        sendWsMessage({ type: 'reset_leaderboard' });
      }
    }

    function renderLeaderboard(entries) {
      if (!Array.isArray(entries) || entries.length === 0) {
        leaderboardBody.innerHTML = '<tr><td colspan="5" style="text-align:center; color:var(--text-muted); padding:16px;">No runs recorded yet. Start Wobble Runner to compete!</td></tr>';
        return;
      }

      let html = '';
      entries.forEach((entry, idx) => {
        const rank = idx + 1;
        let medal = `#${rank}`;
        let rankClass = '';
        if (rank === 1) { medal = '🥇 #1'; rankClass = 'rank-1'; }
        else if (rank === 2) { medal = '🥈 #2'; rankClass = 'rank-2'; }
        else if (rank === 3) { medal = '🥉 #3'; rankClass = 'rank-3'; }

        html += `
          <tr>
            <td class="rank-cell ${rankClass}">${medal}</td>
            <td style="font-weight:700; max-width:90px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;">${escapeHtml(entry.playerName || 'Hero')}</td>
            <td class="score-cell">${(entry.score || 0).toLocaleString()}</td>
            <td class="dist-cell">${(entry.distance || 0).toLocaleString()}m</td>
            <td class="seed-cell">${entry.seed || 0}</td>
          </tr>
        `;
      });
      leaderboardBody.innerHTML = html;
    }

    function escapeHtml(str) {
      return String(str).replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
    }

    // Default view on initial load
    window.addEventListener('DOMContentLoaded', async () => {
      await loadAllViews();
      switchActiveGameView('BalanceTrainer');
      connectWs();
    });
