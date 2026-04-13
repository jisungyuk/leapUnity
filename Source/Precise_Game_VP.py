import random, csv, time, threading, math, os, atexit, tempfile
from pathlib import Path
try:
    from openpyxl import Workbook
except Exception:
    Workbook = None

# ===================== AUDIO PREFS (set BEFORE importing psychopy.sound) =====================
AUDIO_DEVICE_NAME = "VO370M (Intel(R) Display Audio)"
from psychopy import prefs
prefs.hardware['audioLib'] = ['ptb', 'pyo']
prefs.hardware['audioDevice'] = AUDIO_DEVICE_NAME
# ============================================================================================

# Now import PsychoPy modules
from psychopy import visual, core, event, sound, monitors
from psychopy import logging

# ---------------- Input source config ----------------
INPUT_MODE = "labchart"   # "arduino" or "labchart"
ACTIVE_CHANNEL = 2        # 1 = left, 2 = right
MVC_PEAK = 0.0            # 0 disables; else divide raw by MVC_PEAK (±1 clamp)

# ---------------- Trigger (GO sync) config ----------------
TRIG_PORT = "COM3"
TRIG_BAUD = 115200
TRIG_PULSE_MS = 100

# ---------------- Session-level params ----------------
mvc_reps = 3
mvc_hold_sec = 10.0
mvc_rest_sec = 0.0
hold_half_width_frac = 0.025   # kept for config_tail compatibility

# ---------------- Target levels (%MVC) ----------------
target_levels_pct = [20, 40, 60]
MVC_TARGET_PCT = 80

# ---------------- Stimulation session config ----------------
SECOND_TRIGGER_CODE = 2
STIM_BLOCK_SIZE = 20
STIM_TOTAL_TRIALS = 120
STIM_REPEATS_PER_CONDITION = 20
STIM_TARGETS = ("NEAR", "FAR", "NO TARGET")
STIM_FRACTIONS = (25, 75)
STIM_RT_INPUT_MS = {"NEAR": "", "FAR": "", "NO TARGET": ""}
STIM_SECOND_TRIGGER_ENABLED = False
STIM_PLAN = []
STIM_PLAN_PROGRESS = 0
STIM_PLAN_ID = ""
_stim_plan_dir = Path(__file__).resolve().parent if "__file__" in globals() else Path.cwd()
_stim_plan_stamp = time.strftime("%Y%m%d_%H%M%S", time.localtime())
_stim_existing = sorted(_stim_plan_dir.glob(f"Precise_Game_VP_stim_plan_{_stim_plan_stamp}_s*.xlsx"))
STIM_PLAN_SESSION_NUM = len(_stim_existing) + 1
STIM_PLAN_PATH = _stim_plan_dir / f"Precise_Game_VP_stim_plan_{_stim_plan_stamp}_s{STIM_PLAN_SESSION_NUM:02d}.xlsx"
FRO_TEMPLATE_VBS_PATH = Path(r"G:\Shared drives\Cunningham Lab\Studies\LabChart Settings Files\Supplimantary Folder\First.vbs")
FRO_OFF_ON_VBS_PATH = Path(r"G:\Shared drives\Cunningham Lab\Studies\LabChart Settings Files\Supplimantary Folder\OffOn.vbs")
FRO_TEMPLATE_DELAY_S = 0.025

# ---------------- Arduino (optional input) ----------------
try:
    import serial as _serial_mod
    from serial.tools import list_ports as _list_ports_mod
except Exception:
    _serial_mod = None
    _list_ports_mod = None

PORT = None
BAUD = 115200
ser = None
_serial_buf = b""

def find_serial_port(prefer=None):
    if _list_ports_mod is None:
        raise RuntimeError("pyserial required for Arduino (Tools->Package Manager).")
    ports = list(_list_ports_mod.comports())
    if not ports:
        raise RuntimeError("No serial ports found.")
    if prefer:
        for p in ports:
            if prefer.lower() in p.device.lower():
                return p.device
    for p in ports:
        desc = f"{p.device} {p.description}".lower()
        if any(s in desc for s in ("arduino","usb modem","usb serial","ch340")):
            return p.device
    return ports[0].device

def init_serial():
    global ser, PORT
    if ser is not None: return
    if _serial_mod is None: raise RuntimeError("pyserial required for Arduino.")
    PORT = find_serial_port()
    ser = _serial_mod.Serial(PORT, BAUD, timeout=0.001)

def read_latest_serial():
    """Return last Arduino value in [-1,1] or None. Expected 'y,<float>\\n'."""
    global _serial_buf
    if ser is None: return None
    data = ser.read(1024)
    if data: _serial_buf += data
    if not _serial_buf: return None
    lines = _serial_buf.split(b"\\n")
    _serial_buf = lines[-1]
    val = None
    for ln in lines[:-1]:
        try: line = ln.decode("utf-8").strip()
        except: continue
        if not line: continue
        parts = line.split(",")
        if len(parts)==2 and parts[0].lower()=="y":
            try: val = float(parts[1])
            except: pass
    return val

# ---------------- LabChart COM reader (background) ----------------
_lc_stream = None
class LabChartStream:
    """Background reader for LabChart via COM; caches latest per-channel sample."""
    def __init__(self, channels=(1,2), poll_period_s=0.001, coarse_target_hz=3000, window_sec=6.0):
        import pythoncom, win32com.client
        self.pythoncom = pythoncom
        self.win32 = win32com.client
        self.channels = list(channels)
        self.sleep_s = float(poll_period_s)
        self.coarse_target_hz = float(coarse_target_hz)
        self.window_sec = float(window_sec)
        self._th = None
        self._stop = threading.Event()
        self._lock = threading.Lock()
        self._latest = {ch: None for ch in self.channels}
        self._secs_per_tick = None
        self._last_idx = {ch: 0 for ch in self.channels}
        self._block = None
        self._app = None
        self._doc = None

    def start(self):
        if self._th and self._th.is_alive(): return
        self._stop.clear()
        self._th = threading.Thread(target=self._run, name="LabChartStream", daemon=True)
        self._th.start()

    def stop(self):
        if self._th:
            self._stop.set(); self._th.join(timeout=1.0)

    def get_latest(self, ch):
        with self._lock:
            val = self._latest.get(ch, None)
            if val is not None and MVC_PEAK and MVC_PEAK > 0:
                val = max(-1.0, min(1.0, float(val) / MVC_PEAK))
            return val

    def _attach(self):
        try:
            self._app = self.win32.gencache.EnsureDispatch("ADIChart.Application")
        except Exception:
            self._app = self.win32.Dispatch("ADIChart.Application")
        self._doc = self._app.ActiveDocument
        if self._doc is None: raise RuntimeError("No active LabChart document.")
        try: base_records = int(self._doc.NumberOfRecords)
        except Exception: base_records = 0
        self._block = base_records + 1
        self._last_idx = {ch: 0 for ch in self.channels}
        self._update_spt()

    def _update_spt(self):
        try:
            spt = float(self._doc.GetRecordSecsPerTick(self._block))
            if spt > 0:
                self._secs_per_tick = spt
                return
        except Exception:
            pass
        self._secs_per_tick = None

    def _run(self):
        self.pythoncom.CoInitialize()
        try: self._attach()
        except Exception as e:
            print(f"[ERR] LabChart attach failed: {e}", flush=True)
            self.pythoncom.CoUninitialize(); return
        while not self._stop.is_set():
            self.pythoncom.PumpWaitingMessages()
            try: nb = int(self._doc.NumberOfRecords)
            except Exception: nb = 0
            if (nb + 1) != self._block:
                self._block = nb + 1
                self._last_idx = {ch: 0 for ch in self.channels}
                self._update_spt()
            if self._secs_per_tick is None: self._update_spt()
            for ch in self.channels:
                try: data = self._doc.GetChannelData(1, ch, self._block, self._last_idx[ch] + 1, -1)
                except Exception: data = None
                n = len(data) if data is not None else 0
                if n > 0:
                    self._last_idx[ch] += n
                    try: vlast = float(data[-1])
                    except Exception: vlast = None
                    if vlast is not None:
                        with self._lock: self._latest[ch] = vlast
            time.sleep(self.sleep_s)
        self.pythoncom.CoUninitialize()

def init_labchart():
    global _lc_stream
    if _lc_stream is None:
        _lc_stream = LabChartStream(channels=(1,2), poll_period_s=0.001, coarse_target_hz=3000, window_sec=8.0)
        _lc_stream.start()

def stop_labchart():
    global _lc_stream
    if _lc_stream is not None:
        _lc_stream.stop(); _lc_stream = None

def read_latest_labchart(active_channel=2):
    if _lc_stream is None: return None
    return _lc_stream.get_latest(active_channel)

def labchart_start_sampling():
    if INPUT_MODE != "labchart": return
    try:
        import pythoncom, win32com.client
        pythoncom.CoInitialize()
        try:
            app = win32com.client.gencache.EnsureDispatch("ADIChart.Application")
        except Exception:
            app = win32com.client.Dispatch("ADIChart.Application")
        doc = app.ActiveDocument
        if not doc:
            print("[WARN] LabChart: no active document to start sampling."); return
        is_sampling = None
        for attr in ("IsSampling", "Sampling"):
            try:
                is_sampling = bool(getattr(doc, attr)); break
            except Exception: pass
        if is_sampling:
            print("[INFO] LabChart sampling already running.")
        else:
            try: doc.StartSampling(); print("[INFO] LabChart sampling started (StartSampling).")
            except Exception:
                try: doc.Start(); print("[INFO] LabChart sampling started (Start).")
                except Exception as e: print(f"[WARN] LabChart: could not start sampling: {e}")
    except Exception as e:
        print(f"[WARN] LabChart start sampling failed: {e}")
    finally:
        try: import pythoncom as _pc; _pc.CoUninitialize()
        except Exception: pass

def labchart_stop_sampling():
    try:
        import pythoncom, win32com.client
        pythoncom.CoInitialize()
        try:
            app = win32com.client.gencache.EnsureDispatch("ADIChart.Application")
        except Exception:
            app = win32com.client.Dispatch("ADIChart.Application")
        doc = app.ActiveDocument
        if not doc: return
        try: doc.StopSampling(); print("[INFO] LabChart sampling stopped (StopSampling).")
        except Exception:
            try: doc.Stop(); print("[INFO] LabChart sampling stopped (Stop).")
            except Exception: pass
    except Exception:
        pass
    finally:
        try: import pythoncom as _pc; _pc.CoUninitialize()
        except Exception: pass

# ---------------- Trigger serial (separate from Arduino) ----------------
trig_ser = None
_trig_low_at = 0.0  # perf_counter deadline to drop to low
LABCHART_INTERNAL_STIM_ENABLED = True
LABCHART_STIM_REF_OUTPUT = 0
LABCHART_STIM_DELAY_OUTPUT = 1
LABCHART_STIM_PULSE_WIDTH_MS = 1.0
LABCHART_STIM_TRIGGER_CHANNEL = 5
LABCHART_STIM_TRIGGER_THRESHOLD_V = 1.0
LABCHART_STIM_START_DELAY_IDS = ("_StartDelay", "Start Delay")
LABCHART_STIM_PULSE_WIDTH_IDS = ("_PulseWidth", "Pulse Width")
LABCHART_STIM_TRIGGER_CHANNEL_IDS = ("_TriggerChannel", "Trigger Channel", "Threshold Channel")
LABCHART_STIM_TRIGGER_LEVEL_IDS = ("_TriggerLevel", "Trigger Level", "Threshold Level", "_ThresholdLevel")
LABCHART_STIM_BACKUP = {}
_FRO_TEMPLATE_CACHE = None
_FRO_STATE_CHECKED = False
_FRO_STATE_ENABLED = None

def _labchart_stim_attach():
    import pythoncom, win32com.client
    pythoncom.CoInitialize()
    try:
        try:
            app = win32com.client.gencache.EnsureDispatch("ADIChart.Application")
        except Exception:
            app = win32com.client.Dispatch("ADIChart.Application")
        doc = app.ActiveDocument
        if doc is None:
            raise RuntimeError("No active LabChart document.")
        return doc
    except Exception:
        try:
            pythoncom.CoUninitialize()
        except Exception:
            pass
        raise

def _labchart_set_stimulator_value(output_idx, param_ids, value, unit="ms", commit=True, required=True):
    import pythoncom
    import win32com.client.dynamic
    doc = _labchart_stim_attach()
    try:
        last_err = None
        for param_id in param_ids:
            try:
                try:
                    setter = getattr(doc, "SetStimulatorValue")
                    setter(output_idx, param_id, str(value), unit, bool(commit))
                except AttributeError:
                    dyn_doc = win32com.client.dynamic.DumbDispatch(doc)
                    getattr(dyn_doc, "SetStimulatorValue")(output_idx, param_id, str(value), unit, bool(commit))
                return param_id
            except Exception as e:
                last_err = e
        if required:
            raise RuntimeError(f"Could not set stimulator value {param_ids} on output {output_idx}: {last_err}")
        return None
    finally:
        try:
            pythoncom.CoUninitialize()
        except Exception:
            pass

def _labchart_call_method(method_name, *args):
    import pythoncom
    import win32com.client.dynamic
    doc = _labchart_stim_attach()
    try:
        try:
            method = getattr(doc, method_name)
            if callable(method):
                return method(*args)
        except AttributeError:
            method = None
        try:
            dyn_doc = win32com.client.dynamic.DumbDispatch(doc)
            dyn_method = getattr(dyn_doc, method_name)
            if callable(dyn_method):
                return dyn_method(*args)
        except AttributeError:
            pass
        dispid = doc._oleobj_.GetIDsOfNames(method_name)
        return doc._oleobj_.Invoke(dispid, 0, pythoncom.DISPATCH_METHOD, True, *args)
    finally:
        try:
            pythoncom.CoUninitialize()
        except Exception:
            pass

def _load_fro_template_message():
    global _FRO_TEMPLATE_CACHE
    if _FRO_TEMPLATE_CACHE is not None:
        return _FRO_TEMPLATE_CACHE
    if not FRO_TEMPLATE_VBS_PATH.exists():
        raise RuntimeError(f"FRO template macro not found: {FRO_TEMPLATE_VBS_PATH}")
    txt = FRO_TEMPLATE_VBS_PATH.read_text(encoding="utf-8", errors="ignore")
    import re
    m = re.search(r'PlayMessage\s+\("(?P<msg>0x[0-9A-Fa-f]+)"\)', txt)
    if not m:
        raise RuntimeError("Could not find PlayMessage(...) in FRO template macro.")
    _FRO_TEMPLATE_CACHE = bytes.fromhex(m.group("msg")[2:])
    return _FRO_TEMPLATE_CACHE

def _load_fro_playmessages(vbs_path):
    txt = Path(vbs_path).read_text(encoding="utf-8", errors="ignore")
    import re
    matches = re.findall(r'PlayMessage\s+\("(?P<msg>0x[0-9A-Fa-f]+)"\)', txt)
    if not matches:
        raise RuntimeError(f"Could not find PlayMessage(...) in FRO macro: {vbs_path}")
    return ["0x" + msg[2:].upper() for msg in matches]

def _get_fro_template_playmessage():
    return "0x" + _load_fro_template_message().hex().upper()

def _labchart_get_fro_config_text():
    tab_names = (
        "Fast Response Output",
        "Fast Response Outputs",
        "Fast Response",
    )
    last_err = None
    for tab_name in tab_names:
        try:
            txt = _labchart_call_method("GetConfigTabText", tab_name)
            if txt:
                return str(txt)
        except Exception as e:
            last_err = e
    if last_err is not None:
        raise last_err
    return ""

def _fro_outputs_enabled_from_text(cfg_text):
    txt = str(cfg_text or "")
    if not txt.strip():
        return None
    normalized = txt.replace("\r", "")
    output1_on = ("Output = 1\nOn = 1" in normalized) or ("Output 1" in normalized and "On" in normalized)
    output2_on = ("Output = 2\nOn = 1" in normalized) or ("Output 2" in normalized and "On" in normalized)
    if output1_on and output2_on:
        return True
    if ("Output = 1\nOn = 0" in normalized) or ("Output = 2\nOn = 0" in normalized):
        return False
    return None

def _replace_bytes_same_len(buf, old_bytes, new_bytes, occurrence=1):
    if len(old_bytes) != len(new_bytes):
        raise ValueError("Replacement must preserve byte length.")
    start = 0
    idx = -1
    for _ in range(int(occurrence)):
        idx = buf.find(old_bytes, start)
        if idx < 0:
            raise ValueError(f"Could not find target bytes for occurrence {occurrence}.")
        start = idx + len(old_bytes)
    buf[idx:idx+len(old_bytes)] = new_bytes

def _build_fro_playmessage(delay_s, enabled=True):
    raw = bytearray(_load_fro_template_message())
    orig_sum = sum(raw)

    delay_txt = f"{float(delay_s):.3f}"
    if len(delay_txt) != len(f"{FRO_TEMPLATE_DELAY_S:.3f}"):
        raise ValueError(f"Unsupported FRO delay format: {delay_txt}")

    old_delay = f"PulseDelay = {FRO_TEMPLATE_DELAY_S:.3f}".encode("utf-16le")
    new_delay = f"PulseDelay = {delay_txt}".encode("utf-16le")
    _replace_bytes_same_len(raw, old_delay, new_delay, occurrence=1)

    if not enabled:
        _replace_bytes_same_len(raw, "On = 1".encode("utf-16le"), "On = 0".encode("utf-16le"), occurrence=1)
        _replace_bytes_same_len(raw, "On = 1".encode("utf-16le"), "On = 0".encode("utf-16le"), occurrence=2)

    new_sum = sum(raw)
    delta = new_sum - orig_sum
    checksum_val = int.from_bytes(raw[20:24], byteorder="little", signed=False)
    raw[20:24] = int(checksum_val + delta).to_bytes(4, byteorder="little", signed=False)
    return "0x" + raw.hex().upper()

def _labchart_get_stimulator_value(output_idx, param_ids, unit="ms", required=False):
    import pythoncom
    import win32com.client.dynamic
    doc = _labchart_stim_attach()
    try:
        last_err = None
        for param_id in param_ids:
            try:
                try:
                    getter = getattr(doc, "GetStimulatorValue")
                    val = getter(output_idx, param_id, unit)
                except AttributeError:
                    dyn_doc = win32com.client.dynamic.DumbDispatch(doc)
                    val = getattr(dyn_doc, "GetStimulatorValue")(output_idx, param_id, unit)
                return param_id, val
            except Exception as e:
                last_err = e
        if required:
            raise RuntimeError(f"Could not get stimulator value {param_ids} on output {output_idx}: {last_err}")
        return None, None
    finally:
        try:
            pythoncom.CoUninitialize()
        except Exception:
            pass

def _backup_stim_param(output_idx, param_ids, unit="ms"):
    key = (output_idx, tuple(param_ids), unit)
    if key in LABCHART_STIM_BACKUP:
        return
    param_id, value = _labchart_get_stimulator_value(output_idx, param_ids, unit=unit, required=False)
    if param_id is not None:
        LABCHART_STIM_BACKUP[key] = (param_id, value)

def _restore_stim_backup():
    restored_any = False
    for (output_idx, _param_ids, unit), payload in list(LABCHART_STIM_BACKUP.items()):
        param_id, value = payload
        try:
            _labchart_set_stimulator_value(output_idx, (param_id,), value, unit=unit, commit=True, required=False)
            restored_any = True
        except Exception:
            pass
    return restored_any

def labchart_prepare_internal_stim(delay_ms):
    if not LABCHART_INTERNAL_STIM_ENABLED or INPUT_MODE != "labchart":
        return False
    try:
        delay_s = float(delay_ms) / 1000.0
        # Step 1: replay the exact recorded template so FRO returns to the
        # known-good state where both outputs are enabled and Output 1 is immediate.
        _labchart_call_method("PlayMessage", _get_fro_template_playmessage())
        # Step 2: update only Output 2 delay for this trial.
        if abs(delay_s - FRO_TEMPLATE_DELAY_S) > 1e-9:
            core.wait(0.05)
            msg = _build_fro_playmessage(delay_s, enabled=True)
            _labchart_call_method("PlayMessage", msg)
        print(
            f"[INFO] FRO armed from Input 5: output {LABCHART_STIM_REF_OUTPUT + 1} immediate, "
            f"output {LABCHART_STIM_DELAY_OUTPUT + 1} delayed by {float(delay_ms):.3f} ms.",
            flush=True
        )
        return True
    except Exception as e:
        print(f"[WARN] FRO setup failed, using software trigger fallback: {e}", flush=True)
        return False

def labchart_prime_fro(force_check=False):
    global _FRO_STATE_CHECKED, _FRO_STATE_ENABLED
    if not LABCHART_INTERNAL_STIM_ENABLED or INPUT_MODE != "labchart":
        return False
    if _FRO_STATE_CHECKED and not force_check:
        return bool(_FRO_STATE_ENABLED)
    try:
        state_known = None
        try:
            state_known = _fro_outputs_enabled_from_text(_labchart_get_fro_config_text())
        except Exception:
            state_known = None
        if state_known is True:
            _FRO_STATE_CHECKED = True
            _FRO_STATE_ENABLED = True
            print("[INFO] FRO state check: outputs already on.", flush=True)
            return True

        # If available, replay the exact OFF->ON sequence captured from LabChart.
        # That preserves the manual state transition we could not reproduce by
        # only changing the output parameters.
        if FRO_OFF_ON_VBS_PATH.exists():
            for msg in _load_fro_playmessages(FRO_OFF_ON_VBS_PATH):
                _labchart_call_method("PlayMessage", msg)
                core.wait(0.05)
            # OffOn.vbs restores the outputs, but its final state is not the
            # same as our task baseline. Replay the known-good template so
            # Output 1 is immediate again before any per-trial delay updates.
            template_msg = _get_fro_template_playmessage()
            _labchart_call_method("PlayMessage", template_msg)
            core.wait(0.05)
            _FRO_STATE_CHECKED = True
            _FRO_STATE_ENABLED = True
            print("[INFO] FRO primed from OffOn macro.", flush=True)
            return True

        # Fallback for older setups that only have the single template macro.
        template_msg = _get_fro_template_playmessage()
        _labchart_call_method("PlayMessage", template_msg)
        core.wait(0.05)
        _labchart_call_method("PlayMessage", template_msg)
        core.wait(0.05)
        _FRO_STATE_CHECKED = True
        _FRO_STATE_ENABLED = True
        print("[INFO] FRO primed from template macro.", flush=True)
        return True
    except Exception as e:
        print(f"[WARN] Could not prime FRO template state: {e}", flush=True)
        return False

def close_labchart_stim(force=False):
    global _FRO_STATE_CHECKED, _FRO_STATE_ENABLED
    if INPUT_MODE != "labchart":
        return False
    try:
        if _FRO_STATE_CHECKED and _FRO_STATE_ENABLED is False and not force:
            return True
        if FRO_OFF_ON_VBS_PATH.exists():
            msgs = _load_fro_playmessages(FRO_OFF_ON_VBS_PATH)
            if msgs:
                _labchart_call_method("PlayMessage", msgs[0])
                core.wait(0.05)
                _FRO_STATE_CHECKED = True
                _FRO_STATE_ENABLED = False
                print("[INFO] FRO turned off for non-stim mode.", flush=True)
                return True
        msg = _build_fro_playmessage(FRO_TEMPLATE_DELAY_S, enabled=False)
        _labchart_call_method("PlayMessage", msg)
        _FRO_STATE_CHECKED = True
        _FRO_STATE_ENABLED = False
        print("[INFO] FRO turned off for non-stim mode.", flush=True)
        return True
    except Exception:
        pass
    try:
        if _restore_stim_backup():
            print("[INFO] Restored LabChart stimulator settings from runtime backup.", flush=True)
            return True
        return True
    except Exception as e:
        print(f"[WARN] Could not disarm LabChart internal stim: {e}", flush=True)
        return False

def labchart_prepare_fro_for_choice(choice_name):
    if INPUT_MODE != "labchart":
        return False
    stim_needed = bool(choice_name == "stim_session" and STIM_SECOND_TRIGGER_ENABLED)
    if stim_needed:
        return labchart_prime_fro(force_check=True)
    return close_labchart_stim(force=True)

def init_trigger_serial():
    global trig_ser
    if TRIG_PORT and _serial_mod is not None:
        try:
            trig_ser = _serial_mod.Serial(TRIG_PORT, TRIG_BAUD, timeout=0, write_timeout=0.1)
            try:
                trig_ser.reset_output_buffer(); trig_ser.reset_input_buffer()
            except Exception:
                pass
            try:
                trig_ser.setDTR(False); trig_ser.setRTS(False)
            except Exception:
                pass
            try:
                trig_ser.write(b'\x00'); trig_ser.flush()
            except Exception:
                pass
            print(f"[INFO] Trigger serial opened on {TRIG_PORT} @ {TRIG_BAUD}.", flush=True)
        except Exception as e:
            print(f"[WARN] Trigger serial open failed on {TRIG_PORT}: {e}", flush=True)
            trig_ser = None
    elif TRIG_PORT and _serial_mod is None:
        print("[WARN] pyserial not available; trigger disabled.", flush=True)

def send_trigger_high():
    global _trig_low_at
    if trig_ser:
        try:
            trig_ser.write(b'\xff'); trig_ser.flush()
            core.wait(TRIG_PULSE_MS/1000.0)
            trig_ser.write(b'\x00'); trig_ser.flush()
            _trig_low_at = 0.0
        except Exception as e:
            print(f"[WARN] Trigger write high failed: {e}", flush=True)

def send_trigger_code(code):
    if trig_ser:
        try:
            trig_ser.write(bytes([int(code) & 0xFF])); trig_ser.flush()
            core.wait(TRIG_PULSE_MS/1000.0)
            trig_ser.write(b'\x00'); trig_ser.flush()
        except Exception as e:
            print(f"[WARN] Trigger write code {code} failed: {e}", flush=True)

def send_trigger_go():
    send_trigger_code(1)

def dispatch_go_outputs(stim_callback=None, send_go_ttl=True):
    if stim_callback is not None:
        stim_callback()
    if send_go_ttl:
        send_trigger_go()

def service_trigger_low():
    global _trig_low_at
    if trig_ser and _trig_low_at:
        if time.perf_counter() >= _trig_low_at:
            try:
                trig_ser.write(b'\\x00'); trig_ser.flush()
            except Exception as e:
                print(f"[WARN] Trigger write low failed: {e}", flush=True)
            _trig_low_at = 0.0

# ---------------- Unified reader ----------------
def read_latest():
    if INPUT_MODE == "labchart":
        return read_latest_labchart(ACTIVE_CHANNEL)
    else:
        return read_latest_serial()

# ---------------- MVC storage ----------------
mvc_table = {
    "max": {"left": [None, None, None], "right": [None, None, None], "avg_left": None, "avg_right": None},
    "min": {"left": [None, None, None], "right": [None, None, None], "avg_left": None, "avg_right": None},
}

def hand_label(): return "left" if ACTIVE_CHANNEL == 1 else "right"

def have_mvc_avgs(hnd):
    amax = mvc_table["max"]["avg_"+hnd]
    amin = mvc_table["min"]["avg_"+hnd]
    try:
        return (amax is not None) and (amin is not None) and (float(amax) > float(amin))
    except Exception:
        return False

def get_mvc_avgs(hnd):
    if not have_mvc_avgs(hnd): return None
    return float(mvc_table["min"]["avg_"+hnd]), float(mvc_table["max"]["avg_"+hnd])

def recompute_avgs():
    for kind in ("max","min"):
        for hnd in ("left","right"):
            trip = mvc_table[kind][hnd][:max(1, int(mvc_reps))]
            vals = [float(v) for v in trip if isinstance(v,(int,float))]
            mvc_table[kind]["avg_"+hnd] = (sum(vals)/len(vals)) if vals else None

def format_mvc_value(v):
    return "" if v is None else f"{float(v):.6f}"

# ---------------- Window and stimuli ----------------
SCREEN_INDEX = 1  # 0 = primary, 1 = second
DEFAULT_WIN_SIZE = [1280, 720]
mon = monitors.Monitor("lab_monitor")
mon.setSizePix(DEFAULT_WIN_SIZE)
win = visual.Window(fullscr=True, size=DEFAULT_WIN_SIZE, screen=SCREEN_INDEX, color=[0, 0, 0], units="pix", monitor=mon)

ball_vis = visual.Circle(win, radius=14, fillColor="white", lineColor="white", edges=64)
cue_text = visual.TextStim(win, text="", color="yellow", height=40, pos=(-win.size[0]*0.35, 260))
countdown_txt = visual.TextStim(win, text="", color="white", height=64, pos=(0, 330))

# Geometry & UI
half_h = win.size[1] / 2.0
TOP_MARGIN = 40
BOTTOM_MARGIN = 80
BASELINE_Y  = -half_h + BOTTOM_MARGIN
TOP_LIMIT_Y =  half_h - TOP_MARGIN

overall_amp_units = 2.0
gain_pix = (TOP_LIMIT_Y - BASELINE_Y) / overall_amp_units
VISUAL_SCALE = 1.0

# Sound
logging.info(f"Audio prefs -> libs: {prefs.hardware.get('audioLib')}, device: {prefs.hardware.get('audioDevice')}")
from psychopy import sound, logging
logging.console.setLevel(logging.ERROR)

class SilentSound:
    status = None
    def play(self, *args, **kwargs):
        return None
    def stop(self, *args, **kwargs):
        return None

def make_sound_safe(**kwargs):
    try:
        return sound.Sound(**kwargs)
    except Exception as e:
        print(f"[WARN] Audio unavailable, using silent fallback sound: {e}", flush=True)
        return SilentSound()

warn_beep = make_sound_safe(value=600, secs=0.06, sampleRate=44100, stereo=True, volume=0.8)
go_beep   = make_sound_safe(value=1000, secs=0.08, sampleRate=44100, stereo=True, volume=0.9)
try:
    roll_sound = make_sound_safe(value=120, secs=0.12, sampleRate=44100, stereo=True, volume=0.5, loops=-1)
except Exception:
    roll_sound = None

def play_roll_sound():
    if not roll_sound:
        return
    try:
        if getattr(roll_sound, "status", None) != sound.constants.STARTED:
            roll_sound.play(when=win)
    except Exception:
        try:
            roll_sound.play(when=win)
        except Exception:
            pass

def stop_roll_sound():
    if roll_sound:
        try: roll_sound.stop()
        except Exception: pass

# ---------------- Helpers ----------------
invert = False
deadband = 0.03
alpha = 0.8

def ctrl_s_aborted():
    keys = event.getKeys()
    if "lctrl" in keys or "rctrl" in keys:
        t_deadline = time.time() + 0.4
        while time.time() < t_deadline:
            if "s" in event.getKeys():
                return True
            core.wait(0.01)
    return False

def collect_session_baseline(n_frames=120, max_wait_s=12.0):
    vals = []; t0 = time.time()
    while len(vals) < n_frames:
        check_escape_quit()
        if ctrl_s_aborted(): return 0.0
        v = read_latest()
        if v is not None: vals.append(v)
        cue_text.text = "Calibrating... keep stick at rest" + ("" if v is not None else " (waiting for data)")
        cue_text.color = "yellow"; cue_text.draw()
        ball_vis.pos = (0, BASELINE_Y); ball_vis.draw(); win.flip()
        if v is None and (time.time() - t0) > max_wait_s: break
    return (sum(vals)/len(vals)) if vals else 0.0

def get_norm_from_u(u, session_baseline):
    if u is None: return None
    avgs = get_mvc_avgs(hand_label())
    if avgs:
        min_avg, max_avg = avgs
        if max_avg <= min_avg: return None
        norm = (float(u) - min_avg) / (max_avg - min_avg)
        norm = 1.0 - norm if invert else norm
        scale = max(1e-6, float(MVC_TARGET_PCT) / 100.0)
        norm = norm / scale
        return min(1.0, max(0.0, norm))
    d = float(u) - float(session_baseline)
    if abs(d) < deadband: d = 0.0
    d = -d if invert else d
    frac = min(1.0, max(0.0, d / overall_amp_units))
    return frac

def draw_percent_meter(frac, big_text=True):
    axis_x = -win.size[0]*0.40
    axis_top = TOP_LIMIT_Y
    axis_bottom = BASELINE_Y
    axis_w = 6
    axis_rect = visual.Rect(win, width=axis_w, height=(axis_top-axis_bottom), pos=(axis_x, (axis_top+axis_bottom)/2),
                            fillColor="white", lineColor="white", lineWidth=0)
    axis_rect.draw()
    for p in [0,25,50,75,100]:
        y = axis_bottom + (axis_top-axis_bottom)* (p/100.0)
        tick = visual.Rect(win, width=24, height=2, pos=(axis_x+16, y), fillColor="white", lineColor="white", lineWidth=0)
        tick.draw()
        lbl = visual.TextStim(win, text=f"{p}", color="white", height=18, pos=(axis_x+46, y))
        lbl.draw()
    abs_u = max(0.0, min(overall_amp_units, frac * overall_amp_units))
    fill_top = BASELINE_Y + abs_u * ((TOP_LIMIT_Y - BASELINE_Y)/overall_amp_units)
    fill_rect = visual.Rect(win, width=18, height=max(2, fill_top-axis_bottom), pos=(axis_x-22, (fill_top+axis_bottom)/2),
                            fillColor="#33ddff", lineColor="#33ddff", lineWidth=0)
    fill_rect.draw()
    if big_text:
        pct = int(round(frac*100))
        big = visual.TextStim(win, text=f"{pct}%", color="yellow", height=48, pos=(axis_x-22, axis_top+30))
        big.draw()


# ---------------- Robust logging (append-safe) ----------------
LOG_DIR = Path("data"); LOG_DIR.mkdir(parents=True, exist_ok=True)
csv_path = LOG_DIR / "joystick_sessions.csv"
file_exists = csv_path.exists() and csv_path.stat().st_size > 0
csv_file = open(csv_path, "a", newline="", encoding="utf-8")
print(f"[LOG] Appending session data to: {csv_path.resolve()}")
writer = csv.writer(csv_file)

CSV_HEADER = [
    "timestamp_iso",
    "task", "trial", "hand",
    "foreperiod_s", "session_baseline",
    "RT_ms_at_5pct", "rt_onset_time_s_rel_to_go", "mean_abs_move_at_go",
    "bar_level_units", "hold_lower_units", "hold_upper_units", "cum_hold_time_s",
    "go_to_end_s", "trial_duration_s",
    "target_pct", "trial_points", "total_points",
    "MVC_rep", "MVC_max", "MVC_min", "MVC_avg_max", "MVC_avg_min",
    "input_mode", "active_channel", "hold_half_width_frac", "deadband", "alpha",
    "movement_window_sec", "stillness_abs_thresh", "rt_onset_abs_thresh", "bar_abs_thresh",
    "inter_trial_iti", "foreperiod_min", "foreperiod_max",
    "TRIG_PORT", "TRIG_PULSE_MS",
    "VISUAL_SCALE", "TOP_MARGIN", "BOTTOM_MARGIN",
    "target_levels_pct",
    "trained_target_r_px", "target_r_px", "peak_frac", "hit_bool",
    "target_size_level", "hit_rate_10"
]
if not file_exists:
    writer.writerow(CSV_HEADER); csv_file.flush()
    try: os.fsync(csv_file.fileno())
    except Exception: pass

def _flush_fsync(f):
    try:
        f.flush(); os.fsync(f.fileno())
    except Exception:
        pass

def _close_csv():
    _flush_fsync(csv_file)
    try:
        csv_file.close()
    except Exception:
        pass

atexit.register(_close_csv)
atexit.register(labchart_stop_sampling)
atexit.register(close_labchart_stim)

def _now_iso():
    return time.strftime("%Y-%m-%d %H:%M:%S", time.localtime())

def config_tail():
    return [
        INPUT_MODE, ACTIVE_CHANNEL, hold_half_width_frac, deadband, alpha,
        1.0, 0.0, 0.0, 0.0,
        3.0, 0.0, 0.0,
        TRIG_PORT or "", TRIG_PULSE_MS,
        VISUAL_SCALE, TOP_MARGIN, BOTTOM_MARGIN,
        ";".join(str(p) for p in target_levels_pct)
    ]

_LOG_EXTRAS = ["", "", "", "", "", ""]

def set_log_extras(trained_r="", target_r="", peak_frac="", hit_bool="", size_level="", hit_rate_10=""):
    global _LOG_EXTRAS
    _LOG_EXTRAS = [trained_r, target_r, peak_frac, hit_bool, size_level, hit_rate_10]

def _log_row(row):
    writer.writerow(row + list(_LOG_EXTRAS)); _flush_fsync(csv_file)

def _parse_rt_ms(text):
    try:
        val = float(str(text).strip())
        return val if val > 0 else None
    except Exception:
        return None

def stim_rt_values_ms():
    vals = {}
    for name in STIM_TARGETS:
        val = _parse_rt_ms(STIM_RT_INPUT_MS.get(name, ""))
        if val is None:
            return None
        vals[name] = val
    return vals

def stim_can_enable_second_trigger():
    return stim_rt_values_ms() is not None

def stim_plan_in_progress():
    return bool(STIM_PLAN) and 0 < STIM_PLAN_PROGRESS < len(STIM_PLAN)

def stim_trials_remaining():
    return max(0, len(STIM_PLAN) - STIM_PLAN_PROGRESS)

def stim_blocks_completed():
    return STIM_PLAN_PROGRESS // STIM_BLOCK_SIZE

def stim_blocks_remaining():
    remaining = stim_trials_remaining()
    return int(math.ceil(remaining / float(STIM_BLOCK_SIZE))) if remaining else 0

def write_stim_plan_excel():
    if Workbook is None or not STIM_PLAN:
        return
    wb = Workbook()
    ws = wb.active
    ws.title = "Stim Plan"
    ws.append([
        "plan_id", "trial_global", "block_number", "trial_in_block",
        "target", "stim_fraction_pct", "target_rt_ms", "stim_delay_ms",
        "go_trigger_code", "second_trigger_code", "second_trigger_enabled",
        "status", "completed_at"
    ])
    for trial in STIM_PLAN:
        ws.append([
            STIM_PLAN_ID,
            trial["trial_global"],
            trial["block_number"],
            trial["trial_in_block"],
            trial["target"],
            trial["stim_fraction_pct"],
            trial["target_rt_ms"],
            trial["stim_delay_ms"],
            1,
            SECOND_TRIGGER_CODE,
            "yes" if STIM_SECOND_TRIGGER_ENABLED else "no",
            trial.get("status", "pending"),
            trial.get("completed_at", "")
        ])
    try:
        wb.save(STIM_PLAN_PATH)
    except Exception as e:
        fallback_path = STIM_PLAN_PATH.with_name(
            f"{STIM_PLAN_PATH.stem}_autosave_{time.strftime('%Y%m%d_%H%M%S', time.localtime())}{STIM_PLAN_PATH.suffix}"
        )
        try:
            wb.save(fallback_path)
            print(f"[WARN] Could not save stimulation plan Excel file: {e}", flush=True)
            print(f"[INFO] Saved stimulation plan autosave to: {fallback_path}", flush=True)
        except Exception as e2:
            print(f"[WARN] Could not save stimulation plan Excel file: {e}", flush=True)
            print(f"[WARN] Could not save stimulation plan autosave either: {e2}", flush=True)

def generate_stim_plan():
    global STIM_PLAN, STIM_PLAN_PROGRESS, STIM_PLAN_ID
    rt_vals = stim_rt_values_ms()
    if rt_vals is None:
        raise ValueError("All stimulation RT fields must be filled with positive milliseconds.")
    plan = []
    for target_name in STIM_TARGETS:
        for frac_pct in STIM_FRACTIONS:
            delay_ms = int(round(rt_vals[target_name] * (frac_pct / 100.0)))
            for _ in range(STIM_REPEATS_PER_CONDITION):
                plan.append({
                    "target": target_name,
                    "stim_fraction_pct": frac_pct,
                    "target_rt_ms": rt_vals[target_name],
                    "stim_delay_ms": delay_ms,
                })
    random.shuffle(plan)
    for idx, trial in enumerate(plan, start=1):
        trial["trial_global"] = idx
        trial["block_number"] = ((idx - 1) // STIM_BLOCK_SIZE) + 1
        trial["trial_in_block"] = ((idx - 1) % STIM_BLOCK_SIZE) + 1
        trial["status"] = "pending"
        trial["completed_at"] = ""
    STIM_PLAN = plan
    STIM_PLAN_PROGRESS = 0
    STIM_PLAN_ID = time.strftime("stim_%Y%m%d_%H%M%S", time.localtime())
    write_stim_plan_excel()

def reset_stim_plan_state(clear_rt=False):
    global STIM_PLAN, STIM_PLAN_PROGRESS, STIM_PLAN_ID, STIM_SECOND_TRIGGER_ENABLED, STIM_RT_INPUT_MS
    STIM_PLAN = []
    STIM_PLAN_PROGRESS = 0
    STIM_PLAN_ID = ""
    STIM_SECOND_TRIGGER_ENABLED = False
    if clear_rt:
        STIM_RT_INPUT_MS = {name: "" for name in STIM_TARGETS}

# ---------------- Cue sequence ----------------
GO_GREEN = "#66ff66"
def do_pre_go_cues(draw_context_fn, delay_range=(1.0, 2.0), go_flip_callback=None, send_go_ttl=False):
    delay = random.uniform(*delay_range)
    clk = core.Clock()
    cue_text.text = "READY"; cue_text.color = "yellow"
    while clk.getTime() < delay:
        check_escape_quit()
        if ctrl_s_aborted(): return None
        service_trigger_low()
        draw_context_fn()
        cue_text.draw(); win.flip()
    cue_text.text = "GO"; cue_text.color = GO_GREEN
    go_clock = core.Clock()
    win.callOnFlip(go_clock.reset)
    win.callOnFlip(dispatch_go_outputs, go_flip_callback, send_go_ttl)
    go_beep.play(when=win)
    win.flip()
    return go_clock


_shutdown_started = False

def check_escape_quit():
    if "escape" in event.getKeys(["escape"]):
        global _shutdown_started
        if _shutdown_started:
            return True
        _shutdown_started = True
        try: labchart_stop_sampling()
        except Exception: pass
        try: stop_labchart()
        except Exception: pass
        if ser:
            try: ser.close()
            except Exception: pass
        if trig_ser:
            try:
                trig_ser.write(b'\\x00'); trig_ser.close()
            except Exception: pass
        try:
            csv_file.flush(); os.fsync(csv_file.fileno()); csv_file.close()
        except Exception: pass
        try:
            win.close()
        except Exception: pass
        core.quit()
    return False

TRAINED_TARGET_R_PX = None

def _active_target_r_px():
    return float(TRAINED_TARGET_R_PX) if TRAINED_TARGET_R_PX is not None else float(TARGET_R_PX)

# ======================================================================
# Vertical Roll Task (real skee ball task)
# ======================================================================
BALL_RADIUS = 14
BRICK_W, BRICK_H = 80, 16
BRICK_GAP_START = 28
GRAVITY_PX_S2 = 0.85 * (TOP_LIMIT_Y - BASELINE_Y)
ROLL_DRAG_COEFF = 0.30
GROUND_RESTITUTION = 0.0

BRICK_TRAVEL_FRAC = 0.20
BRICK_RESP_ALPHA  = 0.25

MASS_BRICK = 2.0
MASS_BALL  = 1.0
REST_COEFF = 0.15

MAX_TRIAL_S   = 10.0
REST_V_THRESH = 40.0
REST_HOLD_S   = 1.0

TARGET_NEAR   = 0.40
TARGET_MID    = 0.65
TARGET_FAR    = 0.70
TARGET_R_PX   = 36

LANE_TOP_W = 140
LANE_BOTTOM_W = 420
LANE_FILL_COLOR = "#3b2f24"
LANE_EDGE_COLOR = "#c7b299"
LANE_EDGE_WIDTH = 4

LANE_TOP_W = 140
LANE_BOTTOM_W = 420
LANE_FILL_COLOR = "#3b2f24"
LANE_EDGE_COLOR = "#c7b299"
LANE_EDGE_WIDTH = 4

def _draw_lane():
    top_y = TOP_LIMIT_Y
    bot_y = BASELINE_Y
    w_top = LANE_TOP_W
    w_bot = LANE_BOTTOM_W
    verts = [
        (-w_bot/2, bot_y),
        ( w_bot/2, bot_y),
        ( w_top/2, top_y),
        (-w_top/2, top_y)
    ]
    visual.ShapeStim(win, vertices=verts, closeShape=True,
                     fillColor=LANE_FILL_COLOR, lineColor=LANE_EDGE_COLOR,
                     lineWidth=LANE_EDGE_WIDTH).draw()

def _draw_all_targets(span_px):
    for name, frac, r in [("NEAR", TARGET_NEAR, TARGET_R_PX), ("MID", TARGET_MID, TARGET_R_PX), ("FAR", TARGET_FAR, TARGET_R_PX)]:
        y = BASELINE_Y + frac * span_px
        visual.Circle(win, radius=r, pos=(0, y), fillColor=None, lineColor="white", lineWidth=6).draw()
        visual.TextStim(win, text=name, color="white", height=16, pos=(0, y + r + 18)).draw()

def run_vertical_roll(session_baseline, trial_count=15):
    span_px = (TOP_LIMIT_Y - BASELINE_Y)
    ground_y = BASELINE_Y + BALL_RADIUS

    brick_min_y = BASELINE_Y - (BRICK_H + BRICK_GAP_START)
    brick_max_y = BASELINE_Y + BRICK_TRAVEL_FRAC * span_px
    hud_x = min(win.size[0] / 2 - 210, win.size[0] * 0.28)
    hud_wrap = min(360, win.size[0] * 0.32)

    active_r = _active_target_r_px()
    target_defs = [
        {"name": "NEAR", "frac": TARGET_NEAR, "r": active_r, "desc": "Hit the near target.", "mode": "ring"},
        {"name": "FAR", "frac": TARGET_FAR, "r": active_r, "desc": "Hit the far target.", "mode": "ring"},
        {"name": "NO TARGET", "frac": None, "r": 0, "desc": "No target: shoot the ball out of the lane.", "mode": "out"},
    ]

    score_total= visual.TextStim(win, text="", color="white", height=28, pos=(hud_x, 168), wrapWidth=hud_wrap, alignText="left", anchorHoriz="center")
    score_curr = visual.TextStim(win, text="", color="white", height=24, pos=(hud_x, 130), wrapWidth=hud_wrap, alignText="left", anchorHoriz="center")
    trial_txt  = visual.TextStim(win, text="", color="gray",  height=20, pos=(hud_x, 92), wrapWidth=hud_wrap, alignText="left", anchorHoriz="center")
    status_txt = visual.TextStim(win, text="", color="gray", height=17, pos=(hud_x, 56), wrapWidth=hud_wrap, alignText="left", anchorHoriz="center")
    task_desc_txt = visual.TextStim(win, text="", color="yellow", height=22, pos=(0, TOP_LIMIT_Y + 45), wrapWidth=win.size[0] * 0.7)

    def draw_target_only(target_def, color="yellow"):
        if target_def["mode"] == "ring":
            y = BASELINE_Y + target_def["frac"] * span_px
            visual.Circle(win, radius=target_def["r"], pos=(0, y), fillColor=None, lineColor=color, lineWidth=6).draw()
            visual.TextStim(win, text=target_def["name"], color=color, height=18, pos=(0, y + target_def["r"] + 22)).draw()
        else:
            line_y = TOP_LIMIT_Y - 6
            visual.Line(win, start=(-LANE_TOP_W/2, line_y), end=(LANE_TOP_W/2, line_y), lineColor=color, lineWidth=5).draw()
            visual.TextStim(win, text=target_def["name"], color=color, height=24, pos=(0, line_y + 26)).draw()
        task_desc_txt.text = target_def["desc"]
        task_desc_txt.color = color
        task_desc_txt.draw()

    total_pts = 0
    last_trial_pts = 0
    streak = 0
    best_streak = 0
    counted_trials = 0
    last_result_text = "Launch to score"
    if trial_count % len(target_defs) != 0:
        raise ValueError("trial_count must divide evenly across the three target conditions.")
    reps_per_target = trial_count // len(target_defs)
    remaining_by_name = {target_def["name"]: reps_per_target for target_def in target_defs}

    while counted_trials < trial_count:
        display_trial = counted_trials + 1
        available_targets = [target_def for target_def in target_defs if remaining_by_name[target_def["name"]] > 0]
        target_def = random.choice(available_targets)
        tgt_name = target_def["name"]
        tgt_frac = target_def["frac"]
        tgt_r = target_def["r"]

        ball_y = ground_y
        ball_vy = 0.0
        brick_y = brick_min_y
        brick_y_filt = brick_y
        brick_vy = 0.0
        ball_hit = False
        contact_started = False
        contact_active = False

        peak_y = ball_y
        at_rest_clock = None
        out_of_bounds_clock = None
        trial_start_clock = core.Clock()
        aborted_mid_trial = False
        def draw_ready():
            _draw_lane()
            draw_target_only(target_def)
            visual.Rect(win, width=BRICK_W, height=BRICK_H, pos=(0, brick_y + BRICK_H/2), fillColor="#66aaff", lineColor="white").draw()
            ball_vis.pos = (0, ball_y); ball_vis.draw()
            score_total.text = f"Score {total_pts}   Best streak {best_streak}"; score_total.draw()
            score_curr.text = f"Last +{last_trial_pts}   Current streak {streak}"; score_curr.draw()
            trial_txt.text = f"Practice trial {display_trial}/{trial_count}"; trial_txt.draw()
            status_txt.text = f"Target: {tgt_name}\n{last_result_text}"
            status_txt.draw()
            cue_text.draw()

        go_clock = do_pre_go_cues(draw_ready, delay_range=(5.0, 10.0), send_go_ttl=True)
        if go_clock is None:
            peak_frac = 0.0
            trial_pts = 0
            ts = _now_iso(); hand = hand_label()
            set_log_extras()
            _log_row([
                ts, "vertical_roll_aborted", display_trial, hand,
                "", "", "", "", "",
                "", "", "", "",
                "", "",
                (str(int(tgt_frac*100)) if tgt_frac is not None else "OUT"),
                str(trial_pts),
                str(total_pts),
                "", "", "", "", "",
                *config_tail()
            ])
            return

        stop_roll_sound()

        last_t = trial_start_clock.getTime()
        while trial_start_clock.getTime() < MAX_TRIAL_S:
            check_escape_quit(); service_trigger_low()
            if ctrl_s_aborted():
                aborted_mid_trial = True
                break

            now = trial_start_clock.getTime()
            dt = max(1/240.0, min(0.04, now - last_t)); last_t = now

            u = read_latest()
            frac = get_norm_from_u(u, session_baseline) if u is not None else 0.0
            brick_y_target = brick_min_y + frac * (brick_max_y - brick_min_y)
            brick_y_filt = (1 - BRICK_RESP_ALPHA)*brick_y_filt + BRICK_RESP_ALPHA*brick_y_target
            new_brick_y = brick_y_filt
            brick_vy = (new_brick_y - brick_y) / dt
            brick_y = new_brick_y

            # Rolling (no gravity): forward-only with drag
            ball_vy *= (ROLL_DRAG_COEFF ** dt)
            ball_y += ball_vy * dt
            peak_y = max(peak_y, ball_y)

            min_ball_center = BASELINE_Y + BALL_RADIUS
            if ball_y < min_ball_center:
                ball_y = min_ball_center
                if ball_vy < 0:
                    ball_vy = 0.0

            ball_y, ball_vy, brick_vy, contact_started, contact_active, hit_now = handle_single_burst_contact(
                ball_y, ball_vy, brick_y, brick_vy, contact_started, contact_active
            )
            if hit_now:
                if ball_vy > 20:
                    play_roll_sound()
                ball_hit = True

            reset_bar_y = (brick_min_y + ground_y) / 2.0
            if abs(ball_vy) < REST_V_THRESH and ball_hit:
                if at_rest_clock is None:
                    at_rest_clock = core.Clock()
                elif at_rest_clock.getTime() >= REST_HOLD_S and brick_y <= reset_bar_y:
                    break
            else:
                at_rest_clock = None

            if ball_y > TOP_LIMIT_Y and ball_hit:
                if out_of_bounds_clock is None:
                    out_of_bounds_clock = core.Clock()
                elif out_of_bounds_clock.getTime() >= 3.0 and brick_y <= reset_bar_y:
                    break
            else:
                out_of_bounds_clock = None

            _draw_lane()
            draw_ready()  # draws single target and UI
            win.flip()

        stop_roll_sound()

        peak_frac = max(0.0, min(1.0, (peak_y - BASELINE_Y)/span_px))
        bonus_pts = 0
        scored_trial = False
        if ball_hit:
            if target_def["mode"] == "out":
                base_pts = 3
                result_text = "Valid launch!"
            else:
                err = abs(peak_frac - tgt_frac)
                if err <= 0.033:
                    base_pts = 3
                    result_text = "Perfect hit!"
                elif err <= 0.066:
                    base_pts = 2
                    result_text = "Great hit!"
                elif err <= 0.10:
                    base_pts = 1
                    result_text = "Good hit!"
                else:
                    base_pts = 0
                    result_text = "Missed target. Retry this trial."

            if base_pts > 0:
                counted_trials += 1
                remaining_by_name[tgt_name] -= 1
                streak += 1
                best_streak = max(best_streak, streak)
                bonus_pts = min(3, streak // 3)
                trial_pts = base_pts + bonus_pts
                total_pts += trial_pts
                last_trial_pts = trial_pts
                hit_bool = 1
                scored_trial = True
                if bonus_pts > 0:
                    result_text += f" Combo +{bonus_pts}"
            else:
                streak = 0
                trial_pts = 0
                last_trial_pts = 0
                hit_bool = 0
        else:
            streak = 0
            trial_pts = 0
            last_trial_pts = 0
            hit_bool = ""
            result_text = "No launch. Retry this trial."

        last_result_text = result_text

        set_log_extras(
            TRAINED_TARGET_R_PX if TRAINED_TARGET_R_PX is not None else "",
            active_r,
            f"{peak_frac:.4f}",
            str(hit_bool),
            "",
            ""
        )


        ts = _now_iso(); hand = hand_label()
        task_label = "vertical_roll_aborted" if aborted_mid_trial else "vertical_roll"
        _log_row([
            ts, task_label, display_trial, hand,
            "", f"{session_baseline:.4f}",
            "", "", "",
            "", "", "", "",
            "", "",
            (str(int(tgt_frac*100)) if tgt_frac is not None else "OUT"),
            str(trial_pts),
            str(total_pts),
            "", "", "", "", "",
            *config_tail()
        ])

        if aborted_mid_trial:
            fb = core.Clock()
            while fb.getTime() < 0.7:
                check_escape_quit(); service_trigger_low()
                _draw_lane()
                draw_target_only(target_def)
                ball_vis.pos = (0, peak_y); ball_vis.draw()
                score_total.text = f"Score {total_pts}   Best streak {best_streak}"; score_total.draw()
                score_curr.text  = f"Aborted trial   Streak {streak}"; score_curr.draw()
                trial_txt.text = f"Practice trial {display_trial}/{trial_count}"; trial_txt.draw()
                status_txt.text = f"Target: {tgt_name}\nTrial aborted"
                status_txt.draw()
                win.flip()
            break

        fb = core.Clock()
        while fb.getTime() < 1.0:
            check_escape_quit(); service_trigger_low()
            _draw_lane()
            fb_color = "#66ff66" if scored_trial else "red"
            draw_target_only(target_def, color=fb_color)
            visual.Rect(win, width=BRICK_W, height=BRICK_H, pos=(0, brick_y + BRICK_H/2), fillColor="#66aaff", lineColor="white").draw()
            ball_vis.pos = (0, peak_y); ball_vis.draw()
            score_total.text = f"Score {total_pts}   Best streak {best_streak}"; score_total.draw()
            score_curr.text  = f"{result_text}   +{trial_pts}"; score_curr.draw()
            trial_txt.text = f"Practice trial {display_trial}/{trial_count}"; trial_txt.draw()
            status_txt.text = f"Target: {tgt_name}\nPeak: {int(peak_frac*100)}% of lane"
            status_txt.draw()
            win.flip()

        relax = core.Clock()
        while relax.getTime() < 0.8:
            check_escape_quit(); service_trigger_low()
            cue_text.text = "RELAX"; cue_text.color = "yellow"; cue_text.draw()
            win.flip()

    set_log_extras()
    ts = _now_iso(); hand = hand_label()
    _log_row([
        ts, "vertical_roll_summary", "", hand,
        "", f"{session_baseline:.4f}",
        "", "", "",
        "", "", "", "", "", "",
        "", "", "",
        "", "", "", "", "",
        *config_tail()
    ])

def run_stim_session_block(session_baseline):
    global STIM_PLAN_PROGRESS
    if not STIM_PLAN or STIM_PLAN_PROGRESS >= len(STIM_PLAN):
        return

    span_px = (TOP_LIMIT_Y - BASELINE_Y)
    ground_y = BASELINE_Y + BALL_RADIUS
    brick_min_y = BASELINE_Y - (BRICK_H + BRICK_GAP_START)
    brick_max_y = BASELINE_Y + BRICK_TRAVEL_FRAC * span_px
    hud_x = min(win.size[0] / 2 - 210, win.size[0] * 0.28)
    hud_wrap = min(380, win.size[0] * 0.34)
    active_r = _active_target_r_px()

    target_defs = {
        "NEAR": {"name": "NEAR", "frac": TARGET_NEAR, "r": active_r, "desc": "Hit the near target.", "mode": "ring"},
        "FAR": {"name": "FAR", "frac": TARGET_FAR, "r": active_r, "desc": "Hit the far target.", "mode": "ring"},
        "NO TARGET": {"name": "NO TARGET", "frac": None, "r": 0, "desc": "No target: make one valid launch upward.", "mode": "out"},
    }

    progress_txt = visual.TextStim(win, text="", color="white", height=24, pos=(hud_x, 168), wrapWidth=hud_wrap, alignText="left", anchorHoriz="center")
    block_txt = visual.TextStim(win, text="", color="white", height=20, pos=(hud_x, 132), wrapWidth=hud_wrap, alignText="left", anchorHoriz="center")
    trial_txt = visual.TextStim(win, text="", color="gray", height=19, pos=(hud_x, 96), wrapWidth=hud_wrap, alignText="left", anchorHoriz="center")
    status_txt = visual.TextStim(win, text="", color="gray", height=17, pos=(hud_x, 58), wrapWidth=hud_wrap, alignText="left", anchorHoriz="center")
    task_desc_txt = visual.TextStim(win, text="", color="yellow", height=22, pos=(0, TOP_LIMIT_Y + 45), wrapWidth=win.size[0] * 0.7)

    def draw_target_only(target_def, color="yellow"):
        if target_def["mode"] == "ring":
            y = BASELINE_Y + target_def["frac"] * span_px
            visual.Circle(win, radius=target_def["r"], pos=(0, y), fillColor=None, lineColor=color, lineWidth=6).draw()
            visual.TextStim(win, text=target_def["name"], color=color, height=18, pos=(0, y + target_def["r"] + 22)).draw()
        else:
            line_y = TOP_LIMIT_Y - 6
            visual.Line(win, start=(-LANE_TOP_W/2, line_y), end=(LANE_TOP_W/2, line_y), lineColor=color, lineWidth=5).draw()
            visual.TextStim(win, text=target_def["name"], color=color, height=24, pos=(0, line_y + 26)).draw()
        task_desc_txt.text = target_def["desc"]
        task_desc_txt.color = color
        task_desc_txt.draw()

    block_start = STIM_PLAN_PROGRESS
    block_end = min(block_start + STIM_BLOCK_SIZE, len(STIM_PLAN))

    for plan_idx in range(block_start, block_end):
        trial_plan = STIM_PLAN[plan_idx]
        target_def = target_defs[trial_plan["target"]]
        display_trial = trial_plan["trial_global"]
        display_block = trial_plan["block_number"]
        stim_delay_s = trial_plan["stim_delay_ms"] / 1000.0
        completed_global = STIM_PLAN_PROGRESS
        completed_block = plan_idx - block_start
        second_trigger_sent = False
        internal_stim_armed = False

        ball_y = ground_y
        ball_vy = 0.0
        brick_y = brick_min_y
        brick_y_filt = brick_y
        brick_vy = 0.0
        ball_hit = False
        contact_started = False
        contact_active = False
        peak_y = ball_y
        at_rest_clock = None
        out_of_bounds_clock = None
        trial_start_clock = core.Clock()
        aborted_mid_trial = False
        result_text = f"Stim at {trial_plan['stim_fraction_pct']}% RT"

        def draw_ready():
            _draw_lane()
            draw_target_only(target_def)
            visual.Rect(win, width=BRICK_W, height=BRICK_H, pos=(0, brick_y + BRICK_H/2), fillColor="#66aaff", lineColor="white").draw()
            ball_vis.pos = (0, ball_y); ball_vis.draw()
            progress_txt.text = f"Stim trials completed: {completed_global}/{len(STIM_PLAN)}"; progress_txt.draw()
            block_txt.text = f"Block {display_block}/6   Block progress {completed_block}/{STIM_BLOCK_SIZE}"; block_txt.draw()
            trial_txt.text = f"Current target: {target_def['name']}   Planned stim: {trial_plan['stim_fraction_pct']}% RT"; trial_txt.draw()
            status_txt.text = f"Upcoming trial {display_trial}/{len(STIM_PLAN)}"
            status_txt.draw()
            cue_text.draw()

        if STIM_SECOND_TRIGGER_ENABLED:
            internal_stim_armed = labchart_prepare_internal_stim(trial_plan["stim_delay_ms"])
            if not internal_stim_armed:
                print("[INFO] Using software-timed second trigger fallback for this stim trial.", flush=True)

        go_clock = do_pre_go_cues(draw_ready, delay_range=(5.0, 10.0), send_go_ttl=True)
        if go_clock is None:
            return

        stop_roll_sound()
        last_t = trial_start_clock.getTime()
        min_go_wait_s = max(REST_HOLD_S, stim_delay_s + 0.25 if STIM_SECOND_TRIGGER_ENABLED else 0.0)

        while trial_start_clock.getTime() < MAX_TRIAL_S:
            check_escape_quit(); service_trigger_low()
            if ctrl_s_aborted():
                aborted_mid_trial = True
                break

            now = trial_start_clock.getTime()
            dt = max(1/240.0, min(0.04, now - last_t)); last_t = now

            u = read_latest()
            frac = get_norm_from_u(u, session_baseline) if u is not None else 0.0
            brick_y_target = brick_min_y + frac * (brick_max_y - brick_min_y)
            brick_y_filt = (1 - BRICK_RESP_ALPHA)*brick_y_filt + BRICK_RESP_ALPHA*brick_y_target
            new_brick_y = brick_y_filt
            brick_vy = (new_brick_y - brick_y) / dt
            brick_y = new_brick_y

            ball_vy *= (ROLL_DRAG_COEFF ** dt)
            ball_y += ball_vy * dt
            peak_y = max(peak_y, ball_y)

            min_ball_center = BASELINE_Y + BALL_RADIUS
            if ball_y < min_ball_center:
                ball_y = min_ball_center
                if ball_vy < 0:
                    ball_vy = 0.0

            ball_y, ball_vy, brick_vy, contact_started, contact_active, hit_now = handle_single_burst_contact(
                ball_y, ball_vy, brick_y, brick_vy, contact_started, contact_active
            )
            if hit_now:
                if ball_vy > 20:
                    play_roll_sound()
                ball_hit = True

            go_t = go_clock.getTime() if go_clock is not None else 0.0
            if STIM_SECOND_TRIGGER_ENABLED and (not internal_stim_armed) and (not second_trigger_sent) and go_t >= stim_delay_s:
                send_trigger_code(SECOND_TRIGGER_CODE)
                second_trigger_sent = True

            reset_bar_y = (brick_min_y + ground_y) / 2.0
            if abs(ball_vy) < REST_V_THRESH and ball_hit:
                if at_rest_clock is None:
                    at_rest_clock = core.Clock()
                elif at_rest_clock.getTime() >= REST_HOLD_S and brick_y <= reset_bar_y and go_t >= min_go_wait_s:
                    break
            else:
                at_rest_clock = None

            if ball_y > TOP_LIMIT_Y and ball_hit:
                if out_of_bounds_clock is None:
                    out_of_bounds_clock = core.Clock()
                elif out_of_bounds_clock.getTime() >= 3.0 and brick_y <= reset_bar_y and go_t >= min_go_wait_s:
                    break
            else:
                out_of_bounds_clock = None

            draw_ready()
            win.flip()

        stop_roll_sound()
        if aborted_mid_trial:
            return

        peak_frac = max(0.0, min(1.0, (peak_y - BASELINE_Y)/span_px))
        target_hit = False
        if ball_hit:
            if target_def["mode"] == "out":
                target_hit = True
                result_text = "Valid launch recorded"
            else:
                err = abs(peak_frac - target_def["frac"])
                target_y = BASELINE_Y + target_def["frac"] * span_px
                target_hit = abs(peak_y - target_y) <= target_def["r"]
                if err <= 0.033:
                    result_text = "Perfect target hit"
                elif err <= 0.066:
                    result_text = "Great target hit"
                elif err <= 0.10:
                    result_text = "Good target hit"
                else:
                    result_text = "Launch recorded"
        else:
            result_text = "No launch recorded"

        ts = _now_iso(); hand = hand_label()
        _log_row([
            ts, "stim_session", display_trial, hand,
            "", f"{session_baseline:.4f}",
            "", "", "",
            "", "", "", "",
            "", "",
            (str(int(target_def["frac"]*100)) if target_def["frac"] is not None else "OUT"),
            str(trial_plan["stim_fraction_pct"]),
            str(trial_plan["stim_delay_ms"]),
            "", "", "", "", "",
            *config_tail()
        ])

        fb = core.Clock()
        while fb.getTime() < 0.8:
            check_escape_quit(); service_trigger_low()
            _draw_lane()
            draw_target_only(target_def, color=("#66ff66" if target_hit else "red"))
            visual.Rect(win, width=BRICK_W, height=BRICK_H, pos=(0, brick_y + BRICK_H/2), fillColor="#66aaff", lineColor="white").draw()
            ball_vis.pos = (0, peak_y); ball_vis.draw()
            progress_txt.text = f"Stim trials completed: {STIM_PLAN_PROGRESS + 1}/{len(STIM_PLAN)}"; progress_txt.draw()
            block_txt.text = f"Block {display_block}/6   Block progress {completed_block + 1}/{STIM_BLOCK_SIZE}"; block_txt.draw()
            trial_txt.text = f"Target: {target_def['name']}"; trial_txt.draw()
            status_txt.text = f"{result_text}\nDelay: {trial_plan['stim_delay_ms']} ms"
            status_txt.draw()
            win.flip()

        trial_plan["status"] = "completed"
        trial_plan["completed_at"] = ts
        STIM_PLAN_PROGRESS = plan_idx + 1
        write_stim_plan_excel()

# ======================================================================
# Train Target Size (adaptive)
# ======================================================================
def run_train_target_size(session_baseline):
    global TRAINED_TARGET_R_PX
    span_px = (TOP_LIMIT_Y - BASELINE_Y)
    ground_y = BASELINE_Y + BALL_RADIUS

    brick_min_y = BASELINE_Y - (BRICK_H + BRICK_GAP_START)
    brick_max_y = BASELINE_Y + BRICK_TRAVEL_FRAC * span_px

    base_r = float(TARGET_R_PX)
    step_r = 0.3 * base_r  # 30% diameter -> 30% radius
    level = 0
    perf = {}  # level -> list of hits (1/0)
    last_seen = {}  # level -> last trial index
    counts = {}  # level -> {'hit': int, 'miss': int}

    target_frac = TARGET_MID
    target_y = BASELINE_Y + target_frac * span_px

    def draw_target_count_table():
        x = win.size[0] * 0.40
        y_top = min(win.size[1]/2 - 30, TOP_LIMIT_Y - 10)
        visual.TextStim(win, text="Size | H | M", color="gray", height=18, pos=(x, y_top)).draw()
        levels = sorted(counts.keys())
        y = y_top - 24
        for lvl in levels:
            c = counts.get(lvl, {"hit": 0, "miss": 0})
            size_px = base_r + lvl * step_r
            txt = f"{size_px:.0f} | {c['hit']} | {c['miss']}"
            visual.TextStim(win, text=txt, color="white", height=18, pos=(x, y)).draw()
            y -= 22

    trial_idx = 0
    done = False

    while not done:
        radius = base_r + level * step_r
        trial_idx += 1

        ball_y = ground_y
        ball_vy = 0.0
        brick_y = brick_min_y
        brick_y_filt = brick_y
        brick_vy = 0.0
        peak_y = ball_y
        ball_hit = False
        contact_started = False
        contact_active = False
        at_rest_clock = None
        out_of_bounds_clock = None
        trial_start_clock = core.Clock()
        def draw_ready():
            _draw_lane()
            visual.Circle(win, radius=radius, pos=(0, target_y), fillColor=None, lineColor="yellow", lineWidth=6).draw()
            visual.TextStim(win, text="MID", color="yellow", height=18, pos=(0, target_y + radius + 22)).draw()
            visual.Rect(win, width=BRICK_W, height=BRICK_H, pos=(0, brick_y + BRICK_H/2), fillColor="#66aaff", lineColor="white").draw()
            ball_vis.pos = (0, ball_y); ball_vis.draw()
            cue_text.draw()
            hits = perf.get(level, [])
            rate = (sum(hits)/len(hits)*100.0) if hits else 0.0
            visual.TextStim(win, text=f"Train target size | Trial {trial_idx}", color="white", height=24, pos=(win.size[0]*0.33, 70)).draw()
            visual.TextStim(win, text=f"Size: {radius:.1f}px  Hit% (last {len(hits)}): {rate:.0f}%", color="gray", height=20, pos=(win.size[0]*0.33, 40)).draw()
            draw_target_count_table()

        go_clock = do_pre_go_cues(draw_ready, send_go_ttl=True)
        if go_clock is None:
            return

        stop_roll_sound()

        last_t = trial_start_clock.getTime()
        while trial_start_clock.getTime() < MAX_TRIAL_S:
            check_escape_quit(); service_trigger_low()
            if ctrl_s_aborted():
                return

            now = trial_start_clock.getTime()
            dt = max(1/240.0, min(0.04, now - last_t)); last_t = now

            u = read_latest()
            frac = get_norm_from_u(u, session_baseline) if u is not None else 0.0
            brick_y_target = brick_min_y + frac * (brick_max_y - brick_min_y)
            brick_y_filt = (1 - BRICK_RESP_ALPHA)*brick_y_filt + BRICK_RESP_ALPHA*brick_y_target
            new_brick_y = brick_y_filt
            brick_vy = (new_brick_y - brick_y) / dt
            brick_y = new_brick_y

            ball_vy *= (ROLL_DRAG_COEFF ** dt)
            ball_y += ball_vy * dt
            peak_y = max(peak_y, ball_y)

            min_ball_center = BASELINE_Y + BALL_RADIUS
            if ball_y < min_ball_center:
                ball_y = min_ball_center
                if ball_vy < 0:
                    ball_vy = 0.0

            ball_y, ball_vy, brick_vy, contact_started, contact_active, hit_now = handle_single_burst_contact(
                ball_y, ball_vy, brick_y, brick_vy, contact_started, contact_active
            )
            if hit_now:
                if ball_vy > 20:
                    play_roll_sound()
                ball_hit = True

            go_t = go_clock.getTime() if go_clock is not None else 0.0
            reset_bar_y = (brick_min_y + ground_y) / 2.0
            if abs(ball_vy) < REST_V_THRESH and ball_hit:
                if at_rest_clock is None:
                    at_rest_clock = core.Clock()
                elif at_rest_clock.getTime() >= REST_HOLD_S and brick_y <= reset_bar_y and go_t >= min_go_wait_s:
                    break
            else:
                at_rest_clock = None

            if ball_y > TOP_LIMIT_Y and ball_hit:
                if out_of_bounds_clock is None:
                    out_of_bounds_clock = core.Clock()
                elif out_of_bounds_clock.getTime() >= 3.0 and brick_y <= reset_bar_y and go_t >= min_go_wait_s:
                    break
            else:
                out_of_bounds_clock = None

            _draw_lane()
            draw_ready()
            win.flip()

        stop_roll_sound()

        hits = perf.get(level, [])
        hit = abs(peak_y - target_y) <= radius
        if ball_hit:
            hits = perf.setdefault(level, [])
            hits.append(1 if hit else 0)
            if len(hits) > 10:
                hits.pop(0)
            last_seen[level] = trial_idx
            c = counts.setdefault(level, {"hit": 0, "miss": 0})
            if hit:
                c["hit"] += 1
            else:
                c["miss"] += 1

            rate_10 = (sum(hits)/len(hits)) if hits else 0.0
            set_log_extras(
                TRAINED_TARGET_R_PX if TRAINED_TARGET_R_PX is not None else "",
                f"{radius:.1f}",
                "",
                "1" if hit else "0",
                str(level),
                f"{rate_10:.4f}"
            )
            ts = _now_iso(); hand = hand_label()
            _log_row([
                ts, "train_target", trial_idx, hand,
                "", f"{session_baseline:.4f}",
                "", "", "",
                "", "", "", "", "", "",
                str(int(target_frac*100)),
                "", "",
                "", "", "", "",
                *config_tail()
            ])
        else:
            hit = False

        fb = core.Clock()
        while fb.getTime() < 0.8:
            check_escape_quit(); service_trigger_low()
            _draw_lane()
            if ball_hit:
                visual.Circle(win, radius=radius, pos=(0, target_y), fillColor=None, lineColor=("#66ff66" if hit else "red"), lineWidth=6).draw()
                visual.TextStim(win, text=("HIT" if hit else "MISS"), color=("#66ff66" if hit else "red"), height=32, pos=(0, target_y + radius + 32)).draw()
            else:
                visual.Circle(win, radius=radius, pos=(0, target_y), fillColor=None, lineColor="gray", lineWidth=6).draw()
                visual.TextStim(win, text="NO HIT", color="gray", height=32, pos=(0, target_y + radius + 32)).draw()
            rate = (sum(hits)/len(hits)*100.0) if hits else 0.0
            visual.TextStim(win, text=f"Size: {radius:.1f}px  Hit% (last {len(hits)}): {rate:.0f}%", color="gray", height=20, pos=(0, target_y - radius - 28)).draw()
            draw_target_count_table()
            ball_vis.pos = (0, peak_y); ball_vis.draw()
            win.flip()

        if ball_hit:
            # Check any size level that has enough history (rolling 10 for that size)
            candidates = []
            for lvl, hist in perf.items():
                if len(hist) >= 10:
                    rate = sum(hist) / len(hist)
                    if rate > 0.7:
                        candidates.append((last_seen.get(lvl, 0), lvl, rate))

            if candidates:
                candidates.sort(reverse=True)  # most recently updated size wins
                _, best_level, _ = candidates[0]
                TRAINED_TARGET_R_PX = base_r + best_level * step_r
                done = True
            else:
                level += -1 if hit else 1

    done_clk = core.Clock()
    while done_clk.getTime() < 1.2:
        check_escape_quit(); service_trigger_low()
        _draw_lane()
        visual.Circle(win, radius=TRAINED_TARGET_R_PX, pos=(0, target_y), fillColor=None, lineColor="yellow", lineWidth=6).draw()
        visual.TextStim(win, text=f"Trained size set: {TRAINED_TARGET_R_PX:.1f}px", color="yellow", height=24, pos=(0, target_y + TRAINED_TARGET_R_PX + 28)).draw()
        win.flip()
    set_log_extras()

# ======================================================================
# Run Free Play
# ======================================================================
BALL_RADIUS = 14
BRICK_W, BRICK_H = 80, 16
BRICK_GAP_START = 28
GRAVITY_PX_S2 = 0.85 * (TOP_LIMIT_Y - BASELINE_Y)
ROLL_DRAG_COEFF = 0.30
GROUND_RESTITUTION = 0.0

BRICK_TRAVEL_FRAC = 0.20
BRICK_RESP_ALPHA  = 0.25

MASS_BRICK = 2.0
MASS_BALL  = 1.0
REST_COEFF = 0.15

MAX_TRIAL_S   = 99999.0
REST_V_THRESH = 40.0
REST_HOLD_S   = 1.0

def handle_single_burst_contact(ball_y, ball_vy, brick_y, brick_vy, contact_started, contact_active):
    """Allow only the first continuous bar-ball contact burst to impart momentum."""
    brick_top = brick_y + BRICK_H
    ball_bottom = ball_y - BALL_RADIUS
    touching = brick_top >= ball_bottom
    approaching = (brick_vy > ball_vy - 1e-6)
    hit_now = False

    if touching and (not contact_started) and approaching:
        overlap = brick_top - ball_bottom
        if overlap > 0:
            ball_y += overlap + 1.0
        m1, m2 = MASS_BRICK, MASS_BALL
        v1, v2 = brick_vy, ball_vy
        e = REST_COEFF
        new_v1 = (m1 - e*m2)/(m1 + m2)*v1 + (1+e)*m2/(m1 + m2)*v2
        new_v2 = (1+e)*m1/(m1 + m2)*v1 + (m2 - e*m1)/(m1 + m2)*v2
        brick_vy, ball_vy = new_v1, new_v2
        if ball_vy < 0:
            ball_vy = 0.0
        contact_started = True
        contact_active = True
        hit_now = True
    elif contact_started and touching:
        overlap = brick_top - ball_bottom
        if overlap > 0:
            ball_y += overlap + 1.0
        if contact_active and brick_vy > 0 and ball_vy < brick_vy:
            ball_vy = brick_vy
    elif contact_active and (not touching):
        contact_active = False

    return ball_y, ball_vy, brick_vy, contact_started, contact_active, hit_now

TARGET_NEAR   = 0.40
TARGET_MID    = 0.65
TARGET_FAR    = 0.70
TARGET_R_PX   = 36

def _draw_lane():
    top_y = TOP_LIMIT_Y
    bot_y = BASELINE_Y
    w_top = LANE_TOP_W
    w_bot = LANE_BOTTOM_W
    verts = [
        (-w_bot/2, bot_y),
        ( w_bot/2, bot_y),
        ( w_top/2, top_y),
        (-w_top/2, top_y)
    ]
    visual.ShapeStim(win, vertices=verts, closeShape=True,
                     fillColor=LANE_FILL_COLOR, lineColor=LANE_EDGE_COLOR,
                     lineWidth=LANE_EDGE_WIDTH).draw()

def _draw_all_targets(span_px):
    for name, frac, r in [("NEAR", TARGET_NEAR, TARGET_R_PX), ("MID", TARGET_MID, TARGET_R_PX), ("FAR", TARGET_FAR, TARGET_R_PX)]:
        y = BASELINE_Y + frac * span_px
        visual.Circle(win, radius=r, pos=(0, y), fillColor=None, lineColor="white", lineWidth=6).draw()
        visual.TextStim(win, text=name, color="white", height=16, pos=(0, y + r + 18)).draw()

def run_free_play(session_baseline):
    span_px = (TOP_LIMIT_Y - BASELINE_Y)
    ground_y = BASELINE_Y + BALL_RADIUS

    brick_min_y = BASELINE_Y - (BRICK_H + BRICK_GAP_START)
    brick_max_y = BASELINE_Y + BRICK_TRAVEL_FRAC * span_px

    target_defs = [
        ("NEAR", TARGET_NEAR, TARGET_R_PX),
        ("MID",  TARGET_MID,  TARGET_R_PX),
        ("FAR",  TARGET_FAR,  TARGET_R_PX),
    ]

    def draw_target_only(name, frac, r):
        y = BASELINE_Y + frac * span_px
        visual.Circle(win, radius=r, pos=(0, y), fillColor=None, lineColor="yellow", lineWidth=6).draw()
        visual.TextStim(win, text=name, color="yellow", height=18, pos=(0, y + r + 22)).draw()


    tgt_name, tgt_frac, tgt_r = target_defs[1]

    ball_y = ground_y
    ball_vy = 0.0
    brick_y = brick_min_y
    brick_y_filt = brick_y
    brick_vy = 0.0

    peak_y = ball_y
    ball_hit = False
    contact_started = False
    contact_active = False
    at_rest_clock = None
    out_of_bounds_clock = None
    trial_start_clock = core.Clock()
    aborted_mid_trial = False

    stop_roll_sound()

    last_t = trial_start_clock.getTime()
    while trial_start_clock.getTime() < MAX_TRIAL_S:
        check_escape_quit(); service_trigger_low()
        if ctrl_s_aborted():
            aborted_mid_trial = True
            break

        now = trial_start_clock.getTime()
        dt = max(1/240.0, min(0.04, now - last_t)); last_t = now

        u = read_latest()
        frac = get_norm_from_u(u, session_baseline) if u is not None else 0.0
        brick_y_target = brick_min_y + frac * (brick_max_y - brick_min_y)
        brick_y_filt = (1 - BRICK_RESP_ALPHA)*brick_y_filt + BRICK_RESP_ALPHA*brick_y_target
        new_brick_y = brick_y_filt
        brick_vy = (new_brick_y - brick_y) / dt
        brick_y = new_brick_y

        # Rolling (no gravity): forward-only with drag
        ball_vy *= (ROLL_DRAG_COEFF ** dt)
        ball_y += ball_vy * dt
        peak_y = max(peak_y, ball_y)

        min_ball_center = BASELINE_Y + BALL_RADIUS
        if ball_y < min_ball_center:
            ball_y = min_ball_center
            if ball_vy < 0:
                ball_vy = 0.0

        ball_y, ball_vy, brick_vy, contact_started, contact_active, hit_now = handle_single_burst_contact(
            ball_y, ball_vy, brick_y, brick_vy, contact_started, contact_active
        )
        if hit_now:
            if ball_vy > 20:
                play_roll_sound()
            ball_hit = True
            if ball_vy > 0:
                ball_vy *= 2.0
        reset_bar_y = (brick_min_y + ground_y) / 2.0
        if abs(ball_vy) < REST_V_THRESH and ball_hit:
            if at_rest_clock is None:
                at_rest_clock = core.Clock()
            elif at_rest_clock.getTime() >= REST_HOLD_S and brick_y <= reset_bar_y:
                ball_y = ground_y
                ball_vy = 0.0
                peak_y = ball_y
                ball_hit = False
                contact_started = False
                contact_active = False
                at_rest_clock = None
                stop_roll_sound()
        else:
            at_rest_clock = None

        if ball_y > TOP_LIMIT_Y and ball_hit:
            if out_of_bounds_clock is None:
                out_of_bounds_clock = core.Clock()
            elif out_of_bounds_clock.getTime() >= 3.0 and brick_y <= reset_bar_y:
                ball_y = ground_y
                ball_vy = 0.0
                peak_y = ball_y
                ball_hit = False
                contact_started = False
                contact_active = False
                out_of_bounds_clock = None
                stop_roll_sound()
        else:
            out_of_bounds_clock = None
        _draw_lane()
        _draw_all_targets(span_px)  # NEAR/MID/FAR rings, correct size/stroke

        # Brick (the “bar”)
        visual.Rect(
            win, width=BRICK_W, height=BRICK_H,
            pos=(0, brick_y + BRICK_H/2),
            fillColor="#66aaff", lineColor="white"
        ).draw()

        # Ball
        ball_vis.pos = (0, ball_y)
        ball_vis.draw()

        win.flip()


    stop_roll_sound()

    if aborted_mid_trial:
        check_escape_quit(); service_trigger_low()
        win.flip()

# ======================================================================
# MVC (unchanged)
# ======================================================================
def draw_mvc_dynamic_meter(current_mag, running_peak):
    axis_x = -win.size[0]*0.37
    axis_top = TOP_LIMIT_Y
    axis_bottom = BASELINE_Y
    axis_w = 6
    top_val = 1.0
    axis_rect = visual.Rect(win, width=axis_w, height=(axis_top-axis_bottom), pos=(axis_x, (axis_top+axis_bottom)/2),
                            fillColor="white", lineColor="white", lineWidth=0)
    axis_rect.draw()
    for ratio in [0,0.25,0.5,0.75,1.0]:
        y = axis_bottom + (axis_top-axis_bottom)*ratio
        tick = visual.Rect(win, width=24, height=2, pos=(axis_x+16, y), fillColor="white", lineColor="white", lineWidth=0)
        tick.draw()
        val = top_val*ratio
        lbl = visual.TextStim(win, text=f"{val:.3f}", color="white", height=18, pos=(axis_x+76, y))
        lbl.draw()
    cur_ratio = 0.0 if top_val <= 0 else min(1.0, max(0.0, current_mag / top_val))
    fill_top = axis_bottom + (axis_top-axis_bottom)*cur_ratio
    fill_rect = visual.Rect(win, width=18, height=max(2, fill_top-axis_bottom), pos=(axis_x-22, (fill_top+axis_bottom)/2),
                            fillColor="#33ddff", lineColor="#33ddff", lineWidth=0)
    fill_rect.draw()
    max_y = min(axis_top + 30, win.size[1]/2 - 22)
    big_lbl = visual.TextStim(win, text=f"MAX {running_peak:.3f}", color="yellow", height=38, pos=(axis_x+14, max_y))
    big_lbl.draw()

def mvc_preview_mag():
    phase = time.time() * 1.6
    return max(0.0, 0.12 + 0.08 * math.sin(phase) + 0.03 * math.sin(phase * 2.3))

def run_mvc_calibration(session_baseline, initial_slot=None):
    hnd = hand_label()
    set_log_extras()
    span_pix = (TOP_LIMIT_Y - BASELINE_Y)
    mouse = event.Mouse(win=win)
    panel_x = win.size[0] * 0.30
    title = visual.TextStim(win, text=f"MVC Calibration ({hnd.title()} hand)", color="yellow", height=32, pos=(80, 300))
    note = visual.TextStim(win, text="Streaming stays on. Click MVC 1/2/3 to record a 10 s MVC, or Stop to return.", color="gray", height=16, pos=(80, 268))
    status_txt = visual.TextStim(win, text="", color="white", height=22, pos=(panel_x, 220), wrapWidth=360)
    avg_txt = visual.TextStim(win, text="", color="white", height=20, pos=(panel_x, 184), wrapWidth=360)
    stream_txt = visual.TextStim(win, text="", color="yellow", height=16, pos=(80, 230))
    live_val_txt = visual.TextStim(win, text="", color="white", height=16, pos=(80, 204))
    rep_buttons = [
        visual.Rect(win, width=280, height=56, pos=(panel_x, 98)),
        visual.Rect(win, width=280, height=56, pos=(panel_x, 26)),
        visual.Rect(win, width=280, height=56, pos=(panel_x, -46)),
    ]
    stop_btn = visual.Rect(win, width=280, height=56, pos=(panel_x, -156))
    last_message = "Pick an MVC slot to record."
    pending_slot = initial_slot
    current_mag = 0.0
    running_peak = 1e-6
    raw_live_value = None
    preview_mode = False

    def draw_rep_summary():
        for idx in range(3):
            mx = format_mvc_value(mvc_table["max"][hnd][idx])
            mn = format_mvc_value(mvc_table["min"][hnd][idx])
            label = f"MVC {idx+1}   Max {mx or '-'}   Min {mn or '-'}"
            hover = _point_in_rect(*mouse.getPos(), rep_buttons[idx])
            _draw_button(win, rep_buttons[idx], label, hover=hover)
        hover_stop = _point_in_rect(*mouse.getPos(), stop_btn)
        _draw_button(win, stop_btn, "Stop MVC Calibration", hover=hover_stop)
        avg_txt.text = (
            f"Avg Max: {format_mvc_value(mvc_table['max']['avg_'+hnd]) or '-'}   "
            f"Avg Min: {format_mvc_value(mvc_table['min']['avg_'+hnd]) or '-'}"
        )
        avg_txt.draw()

    def draw_idle_screen():
        idle_frac = min(1.0, max(0.0, current_mag))
        ball_vis.pos = (0.0, BASELINE_Y + idle_frac * span_pix)
        draw_mvc_dynamic_meter(current_mag, running_peak)
        title.draw(); note.draw()
        status_txt.text = last_message; status_txt.draw()
        stream_txt.text = "Preview mode: no live LabChart data" if preview_mode else "Live data stream detected"
        stream_txt.color = "yellow" if preview_mode else "#66ff66"
        stream_txt.draw()
        if raw_live_value is None:
            live_val_txt.text = "Current raw value: --"
        else:
            live_val_txt.text = f"Current raw value: {raw_live_value:.6f}"
        live_val_txt.draw()
        draw_rep_summary()
        cue_text.text = ""; cue_text.color = "yellow"
        countdown_txt.text = ""
        ball_vis.draw()
        cue_text.draw()

    def record_rep(rep_idx):
        nonlocal last_message, running_peak, raw_live_value
        live_data_seen = False
        running_peak = max(running_peak, current_mag, 1e-6)

        t0 = core.Clock()
        mvc_max_raw = -1e30
        mvc_min_raw = +1e30

        while t0.getTime() < mvc_hold_sec:
            check_escape_quit()
            if ctrl_s_aborted(): return "aborted"
            service_trigger_low()
            u = read_latest()
            mag = 0.0
            if u is not None:
                live_data_seen = True
                try:
                    val = float(u)
                    raw_live_value = val
                    diff = val - float(session_baseline)
                    mvc_max_raw = max(mvc_max_raw, val)
                    mvc_min_raw = min(mvc_min_raw, val)
                    mag = abs(diff)
                except Exception:
                    mag = 0.0
            else:
                mag = mvc_preview_mag()
            if mag > running_peak:
                running_peak = mag

            frac = min(1.0, max(0.0, mag))
            y_pix = BASELINE_Y + frac * span_pix

            remain = mvc_hold_sec - t0.getTime()
            countdown_txt.text = f"{int(math.ceil(max(0.0, remain)))}"
            countdown_txt.color = "white"

            draw_mvc_dynamic_meter(mag, running_peak)
            title.draw(); note.draw()
            status_txt.text = f"Recording MVC {rep_idx+1}"; status_txt.draw()
            stream_txt.text = "Preview mode: no live LabChart data" if u is None else "Live data stream detected"
            stream_txt.color = "yellow" if u is None else "#66ff66"
            stream_txt.draw()
            live_val_txt.text = f"Current raw value: {raw_live_value:.6f}" if raw_live_value is not None else "Current raw value: --"
            live_val_txt.draw()
            draw_rep_summary()
            countdown_txt.draw()
            cue_text.text = f"MVC {rep_idx+1}"; cue_text.color = GO_GREEN
            cue_text.draw()
            ball_vis.pos = (0.0, y_pix); ball_vis.draw()
            win.flip()

        if not live_data_seen:
            last_message = f"MVC {rep_idx+1} preview only. No live data, so slot not updated."
            return "done"

        if mvc_max_raw <= -1e29: mvc_max_raw = None
        if mvc_min_raw >= +1e29: mvc_min_raw = None

        mvc_table["max"][hnd][rep_idx] = mvc_max_raw
        mvc_table["min"][hnd][rep_idx] = mvc_min_raw
        recompute_avgs()
        last_message = f"MVC {rep_idx+1} updated."

        ts = _now_iso()
        _log_row([
            ts, "mvc", "", hnd,
            "", f"{session_baseline:.4f}",
            "", "", "",
            "", "", "", "", "", "",
            rep_idx + 1,
            format_mvc_value(mvc_max_raw),
            format_mvc_value(mvc_min_raw),
            format_mvc_value(mvc_table['max']['avg_'+hnd]),
            format_mvc_value(mvc_table['min']['avg_'+hnd]),
            *config_tail()
        ])
        return "done"

    while True:
        check_escape_quit()
        if ctrl_s_aborted(): return "aborted"
        service_trigger_low()
        u = read_latest()
        current_mag = 0.0
        raw_live_value = None
        preview_mode = (u is None)
        if u is not None:
            try:
                raw_live_value = float(u)
                current_mag = abs(float(u) - float(session_baseline))
            except Exception:
                current_mag = 0.0
        else:
            current_mag = mvc_preview_mag()
        running_peak = max(running_peak, current_mag, 1e-6)

        draw_idle_screen()
        win.flip()

        if pending_slot is not None:
            result = record_rep(int(pending_slot))
            pending_slot = None
            if result == "aborted":
                return "aborted"
            continue

        if mouse.getPressed()[0]:
            core.wait(0.12)
            mx, my = mouse.getPos()
            for idx, rect in enumerate(rep_buttons):
                if _point_in_rect(mx, my, rect):
                    result = record_rep(idx)
                    if result == "aborted":
                        return "aborted"
                    break
            else:
                if _point_in_rect(mx, my, stop_btn):
                    break

    ts = _now_iso()
    _log_row([
        ts, "mvc_summary", "", hnd,
        "", f"{session_baseline:.4f}",
        "", "", "",
        "", "", "", "", "", "",
        "", "", "",
        f"{mvc_table['max']['avg_'+hnd]:.6f}" if mvc_table['max']['avg_'+hnd] is not None else "",
        f"{mvc_table['min']['avg_'+hnd]:.6f}" if mvc_table['min']['avg_'+hnd] is not None else "",
        *config_tail()
    ])
    return "done"

# ---------------- Menu ----------------
def _draw_button(win, rect, label, hover=False):
    rect.fillColor = "#3a3a3a" if not hover else "#5a5a5a"
    rect.lineColor = "white"; rect.lineWidth = 1
    rect.draw()
    visual.TextStim(win, text=label, color="white", height=26, pos=rect.pos).draw()

def _draw_checkbox(win, rect, label, checked=False, enabled=True, hover=False):
    rect.fillColor = "#3a3a3a" if enabled else "#222222"
    rect.lineColor = ("yellow" if hover and enabled else "white")
    rect.lineWidth = 2
    rect.draw()
    if checked:
        inset = min(rect.width, rect.height) * 0.28
        visual.Rect(win, width=rect.width - inset, height=rect.height - inset, pos=rect.pos,
                    fillColor="yellow" if enabled else "gray", lineColor=None).draw()
    visual.TextStim(win, text=label, color=("white" if enabled else "gray"),
                    height=16, pos=(rect.pos[0] + 120, rect.pos[1])).draw()

def _point_in_rect(px, py, rect):
    cx, cy = rect.pos; w, h = rect.width, rect.height
    return (cx - w/2 <= px <= cx + w/2) and (cy - h/2 <= py <= cy + h/2)

def settle_labchart(seconds=3.0, prompt="Preparing (stabilizing signal)..."):
    countdown_txt.color = "white"
    cue_text.color = "yellow"
    cue_text.text = prompt
    clk = core.Clock()
    while clk.getTime() < seconds:
        check_escape_quit()
        if ctrl_s_aborted(): return
        remaining = max(0.0, seconds - clk.getTime())
        countdown_txt.text = f"{int(math.ceil(remaining))}"
        ball_vis.pos = (0.0, BASELINE_Y); ball_vis.draw()
        if have_mvc_avgs(hand_label()): draw_percent_meter(0.0, big_text=False)
        countdown_txt.draw(); cue_text.draw(); win.flip()

def show_task_menu(win):
    """
    Returns one of: 'vertical_roll', 'mvc', 'mvc_slot_1', 'mvc_slot_2', 'mvc_slot_3', 'stim_session'. Esc quits.
    """
    global INPUT_MODE, ACTIVE_CHANNEL, MVC_TARGET_PCT, STIM_RT_INPUT_MS, STIM_SECOND_TRIGGER_ENABLED

    mouse = event.Mouse(win=win)
    w, h = win.size
    lane = int(min(360, w * 0.22))
    left_x, mid_x, right_x = -lane * 1.45, 0, lane * 1.45

    title_y = min(h/2 - 80, 300)
    note_y = title_y - 32
    title = visual.TextStim(win, text="Select Task", color="yellow", height=40, pos=(0, title_y))
    note  = visual.TextStim(win, text="1=Practice Game  2=MVC  3=Stim Session   Esc=Quit | Ctrl+S abort in-task",
                            color="gray", height=16, pos=(0, note_y))

    btn_w, btn_h = 320, 68
    roll_btn = visual.Rect(win, width=btn_w, height=btn_h, pos=(left_x, 150))
    mvc_btn  = visual.Rect(win, width=btn_w, height=btn_h, pos=(left_x, 68))
    stim_btn = visual.Rect(win, width=btn_w, height=btn_h, pos=(left_x, -14))
    stim_reset_btn = visual.Rect(win, width=btn_w, height=50, pos=(left_x, -166))
    mvc_pct_box = visual.Rect(win, width=170, height=40, pos=(left_x, -236))
    mvc_pct_label = visual.TextStim(win, text="MVC % for full scale", color="gray", height=15, pos=(left_x, -206))
    mvc_input_str = str(int(MVC_TARGET_PCT))
    editing_field = None
    stim_rt_inputs = dict(STIM_RT_INPUT_MS)
    manual_mvc_inputs = {
        "left": {
            "max": format_mvc_value(mvc_table["max"]["avg_left"]),
            "min": format_mvc_value(mvc_table["min"]["avg_left"]),
        },
        "right": {
            "max": format_mvc_value(mvc_table["max"]["avg_right"]),
            "min": format_mvc_value(mvc_table["min"]["avg_right"]),
        },
    }

    right_hdr  = visual.TextStim(win, text="Source & Hand", color="white", height=21, pos=(right_x, 214))
    src_arduino = visual.Rect(win, width=132, height=40, pos=(right_x - 72, 162))
    src_labchart= visual.Rect(win, width=132, height=40, pos=(right_x + 72, 162))
    hand_left  = visual.Rect(win, width=132, height=40, pos=(right_x - 72, 116))
    hand_right = visual.Rect(win, width=132, height=40, pos=(right_x + 72, 116))
    mvc_manual_hdr = visual.TextStim(win, text="Direct MVC Entry", color="white", height=21, pos=(right_x, 56))
    mvc_max_lbl = visual.TextStim(win, text="MVC Max", color="white", height=17, pos=(right_x - 82, 18))
    mvc_max_box = visual.Rect(win, width=138, height=38, pos=(right_x + 62, 18))
    mvc_min_lbl = visual.TextStim(win, text="MVC Min", color="white", height=17, pos=(right_x - 82, -30))
    mvc_min_box = visual.Rect(win, width=138, height=38, pos=(right_x + 62, -30))
    mvc_manual_note = visual.TextStim(win, text="", color="gray", height=14, pos=(right_x, -68), wrapWidth=300)
    stim_hdr = visual.TextStim(win, text="Stim Session Setup", color="white", height=21, pos=(right_x, -108))
    rt_near_lbl = visual.TextStim(win, text="Near RT (ms)", color="white", height=16, pos=(right_x, -138))
    rt_near_box = visual.Rect(win, width=184, height=36, pos=(right_x, -162))
    rt_far_lbl = visual.TextStim(win, text="Far RT (ms)", color="white", height=16, pos=(right_x, -198))
    rt_far_box = visual.Rect(win, width=184, height=36, pos=(right_x, -222))
    rt_none_lbl = visual.TextStim(win, text="No Target RT (ms)", color="white", height=16, pos=(right_x, -258))
    rt_none_box = visual.Rect(win, width=184, height=36, pos=(right_x, -282))
    trig_checkbox = visual.Rect(win, width=24, height=24, pos=(right_x - 128, -324))
    trig_lbl_1 = visual.TextStim(win, text="Enable 2nd trigger", color="white", height=16, pos=(right_x + 18, -316))
    trig_lbl_2 = visual.TextStim(win, text="Generate randomized 120-trial plan", color="gray", height=14, pos=(right_x + 52, -338))
    mvc_slot_hdr = visual.TextStim(win, text="Stored MVCs", color="white", height=21, pos=(mid_x, 214))
    mvc_slot_rects = [
        visual.Rect(win, width=290, height=40, pos=(mid_x, 162)),
        visual.Rect(win, width=290, height=40, pos=(mid_x, 114)),
        visual.Rect(win, width=290, height=40, pos=(mid_x, 66)),
    ]
    mvc_avg_txt = visual.TextStim(win, text="", color="gray", height=16, pos=(mid_x, 20))

    def _draw_toggle(rect, label, selected):
        rect.fillColor = ("#5a5a5a" if selected else "#3a3a3a")
        rect.lineColor = "white"; rect.lineWidth = 2; rect.draw()
        visual.TextStim(win, text=label, color=("yellow" if selected else "white"),
                        height=18, pos=rect.pos).draw()

    def _parse_manual_mvc(text):
        try:
            return float(str(text).strip())
        except Exception:
            return None

    def _commit_manual_mvc(hnd):
        max_val = _parse_manual_mvc(manual_mvc_inputs[hnd]["max"])
        min_val = _parse_manual_mvc(manual_mvc_inputs[hnd]["min"])
        if max_val is None or min_val is None:
            return False
        if max_val <= min_val:
            return False
        mvc_table["max"]["avg_" + hnd] = max_val
        mvc_table["min"]["avg_" + hnd] = min_val
        return True

    while True:
        check_escape_quit(); service_trigger_low()
        keys = event.getKeys()
        mouse = event.Mouse(win=win); mx, my = mouse.getPos()

        if editing_field is not None:
            for k in keys:
                if k in ("return", "num_enter", "enter"):
                    if editing_field == "mvc":
                        try:
                            val = int(mvc_input_str) if mvc_input_str else int(MVC_TARGET_PCT)
                            MVC_TARGET_PCT = max(1, min(100, val))
                        except Exception:
                            pass
                        mvc_input_str = str(int(MVC_TARGET_PCT))
                    elif editing_field in ("mvc_max", "mvc_min"):
                        _commit_manual_mvc(hand_label())
                    else:
                        target_name = editing_field
                        stim_rt_inputs[target_name] = stim_rt_inputs[target_name].strip()
                        STIM_RT_INPUT_MS = dict(stim_rt_inputs)
                    editing_field = None
                elif k == "backspace":
                    if editing_field == "mvc":
                        mvc_input_str = mvc_input_str[:-1]
                    elif editing_field in ("mvc_max", "mvc_min"):
                        key_name = "max" if editing_field == "mvc_max" else "min"
                        manual_mvc_inputs[hand_label()][key_name] = manual_mvc_inputs[hand_label()][key_name][:-1]
                    else:
                        stim_rt_inputs[editing_field] = stim_rt_inputs[editing_field][:-1]
                elif editing_field in ("mvc_max", "mvc_min") and k in ("-", "minus"):
                    key_name = "max" if editing_field == "mvc_max" else "min"
                    curr = manual_mvc_inputs[hand_label()][key_name]
                    if not curr:
                        manual_mvc_inputs[hand_label()][key_name] = "-"
                elif editing_field in ("mvc_max", "mvc_min") and k in (".", "period", "num_decimal", "decimal"):
                    key_name = "max" if editing_field == "mvc_max" else "min"
                    if "." not in manual_mvc_inputs[hand_label()][key_name]:
                        manual_mvc_inputs[hand_label()][key_name] = (manual_mvc_inputs[hand_label()][key_name] + ".")[:12]
                elif editing_field != "mvc" and k in (".", "period", "num_decimal", "decimal"):
                    if "." not in stim_rt_inputs[editing_field]:
                        stim_rt_inputs[editing_field] = (stim_rt_inputs[editing_field] + ".")[:8]
                elif k.isdigit():
                    if editing_field == "mvc":
                        mvc_input_str = (mvc_input_str + k)[:3]
                    elif editing_field in ("mvc_max", "mvc_min"):
                        key_name = "max" if editing_field == "mvc_max" else "min"
                        manual_mvc_inputs[hand_label()][key_name] = (manual_mvc_inputs[hand_label()][key_name] + k)[:12]
                    else:
                        stim_rt_inputs[editing_field] = (stim_rt_inputs[editing_field] + k)[:8]

        if editing_field is None:
            if "1" in keys or "num_1" in keys:
                _commit_manual_mvc(hand_label())
                return "vertical_roll"
            if "2" in keys or "num_2" in keys:
                _commit_manual_mvc(hand_label())
                return "mvc"
            if "3" in keys or "num_3" in keys:
                _commit_manual_mvc(hand_label())
                return "stim_session"

        rt_ready = all(_parse_rt_ms(stim_rt_inputs.get(name, "")) is not None for name in STIM_TARGETS)
        stim_plan_locked = stim_plan_in_progress()
        rt_locked = STIM_SECOND_TRIGGER_ENABLED or stim_plan_locked

        hover_roll = _point_in_rect(mx, my, roll_btn)
        hover_mvc  = _point_in_rect(mx, my, mvc_btn)
        hover_stim = _point_in_rect(mx, my, stim_btn)
        hover_stim_reset = _point_in_rect(mx, my, stim_reset_btn)
        hover_mvcpct = _point_in_rect(mx, my, mvc_pct_box)
        hover_sa   = _point_in_rect(mx, my, src_arduino)
        hover_sl   = _point_in_rect(mx, my, src_labchart)
        hover_hl   = _point_in_rect(mx, my, hand_left)
        hover_hr   = _point_in_rect(mx, my, hand_right)
        hover_mvc_max = _point_in_rect(mx, my, mvc_max_box)
        hover_mvc_min = _point_in_rect(mx, my, mvc_min_box)
        hover_rt_near = _point_in_rect(mx, my, rt_near_box)
        hover_rt_far = _point_in_rect(mx, my, rt_far_box)
        hover_rt_none = _point_in_rect(mx, my, rt_none_box)
        hover_trig = _point_in_rect(mx, my, trig_checkbox)
        hover_mvc_slots = [_point_in_rect(mx, my, rect) for rect in mvc_slot_rects]

        title.draw(); note.draw()
        _draw_button(win, roll_btn, "Start Practice Game", hover=hover_roll)
        _draw_button(win, mvc_btn,  "Start MVC Calibration", hover=hover_mvc)
        _draw_button(win, stim_btn, "Start Stim Session (20-trial block)", hover=hover_stim)
        _draw_button(win, stim_reset_btn, "Reset Stim Plan / RT Setup", hover=hover_stim_reset)
        mvc_pct_label.draw()
        mvc_pct_box.fillColor = ("#5a5a5a" if hover_mvcpct or editing_field == "mvc" else "#3a3a3a")
        mvc_pct_box.lineColor = "white"; mvc_pct_box.lineWidth = 1; mvc_pct_box.draw()
        visual.TextStim(win, text=f"{mvc_input_str}%", color="white", height=20, pos=mvc_pct_box.pos).draw()

        right_hdr.draw()
        _draw_toggle(src_arduino, "Arduino", INPUT_MODE=="arduino")
        _draw_toggle(src_labchart, "LabChart", INPUT_MODE=="labchart")
        _draw_toggle(hand_left,  "Left (ch 1)", ACTIVE_CHANNEL==1)
        _draw_toggle(hand_right, "Right (ch 2)", ACTIVE_CHANNEL==2)

        mvc_manual_hdr.draw()
        mvc_max_lbl.draw(); mvc_min_lbl.draw()
        for rect, label_key, hover_now in (
            (mvc_max_box, "max", hover_mvc_max),
            (mvc_min_box, "min", hover_mvc_min),
        ):
            active_edit = (editing_field == ("mvc_" + label_key))
            rect.fillColor = "#5a5a5a" if (hover_now or active_edit) else "#3a3a3a"
            rect.lineColor = "white"
            rect.lineWidth = 1
            rect.draw()
            visual.TextStim(
                win,
                text=manual_mvc_inputs[hand_label()][label_key],
                color="white",
                height=18,
                pos=rect.pos
            ).draw()
        if have_mvc_avgs(hand_label()):
            mvc_manual_note.text = f"Current {hand_label()} avg: max {format_mvc_value(mvc_table['max']['avg_' + hand_label()])}   min {format_mvc_value(mvc_table['min']['avg_' + hand_label()])}"
            mvc_manual_note.color = "gray"
        else:
            mvc_manual_note.text = f"Enter {hand_label()} max/min, then press Enter in a box."
            mvc_manual_note.color = "yellow"
        mvc_manual_note.draw()

        stim_hdr.draw()
        rt_near_lbl.draw(); rt_far_lbl.draw(); rt_none_lbl.draw()
        for rect, key_name, hover_now in (
            (rt_near_box, "NEAR", hover_rt_near),
            (rt_far_box, "FAR", hover_rt_far),
            (rt_none_box, "NO TARGET", hover_rt_none),
        ):
            rect.fillColor = "#5a5a5a" if (hover_now or editing_field == key_name) and not rt_locked else "#3a3a3a"
            rect.lineColor = "white" if not rt_locked else "gray"
            rect.lineWidth = 1
            rect.draw()
            txt = stim_rt_inputs[key_name]
            visual.TextStim(win, text=(f"{txt} ms" if txt else ""), color=("white" if not rt_locked else "gray"), height=18, pos=rect.pos).draw()

        _draw_checkbox(
            win, trig_checkbox, "",
            checked=STIM_SECOND_TRIGGER_ENABLED, enabled=(rt_ready and not stim_plan_locked), hover=hover_trig
        )
        trig_lbl_1.color = ("white" if (rt_ready and not stim_plan_locked) else "gray")
        trig_lbl_2.color = ("gray" if (rt_ready and not stim_plan_locked) else "#666666")
        trig_lbl_1.draw()
        trig_lbl_2.draw()
        status_y = -372
        if stim_plan_locked:
            visual.TextStim(win, text="Stim settings locked while a plan is in progress.", color="yellow", height=16, pos=(right_x, status_y)).draw()
        elif STIM_SECOND_TRIGGER_ENABLED:
            visual.TextStim(win, text="RT fields locked while the stimulation plan is armed.", color="yellow", height=16, pos=(right_x, status_y)).draw()
        elif not rt_ready:
            visual.TextStim(win, text="Fill all three RT boxes to enable the 2nd trigger checkbox.", color="gray", height=16, pos=(right_x, status_y)).draw()
        else:
            visual.TextStim(win, text="Second trigger disabled.", color="gray", height=16, pos=(right_x, status_y)).draw()

        progress_text = "No stimulation plan generated yet."
        if STIM_PLAN:
            progress_text = f"Trials left: {stim_trials_remaining()}   Blocks left: {stim_blocks_remaining()}"
        visual.TextStim(win, text=progress_text, color="white", height=15, pos=(right_x, -400), wrapWidth=320).draw()
        visual.TextStim(win, text=f"Plan file: {STIM_PLAN_PATH.name}", color="gray", height=13, pos=(right_x, -424), wrapWidth=320).draw()

        mvc_slot_hdr.draw()
        hnd = hand_label()
        for idx, rect in enumerate(mvc_slot_rects):
            rect.fillColor = "#5a5a5a" if hover_mvc_slots[idx] else "#3a3a3a"
            rect.lineColor = "white"
            rect.lineWidth = 1
            rect.draw()
            label = (
                f"MVC {idx+1}   Max: {format_mvc_value(mvc_table['max'][hnd][idx]) or '-'}   "
                f"Min: {format_mvc_value(mvc_table['min'][hnd][idx]) or '-'}"
            )
            visual.TextStim(win, text=label, color="white", height=15, pos=rect.pos).draw()
        mvc_avg_txt.text = (
            f"Avg Max: {format_mvc_value(mvc_table['max']['avg_'+hnd]) or '-'}   "
            f"Avg Min: {format_mvc_value(mvc_table['min']['avg_'+hnd]) or '-'}"
        )
        mvc_avg_txt.draw()

        win.flip()

        if mouse.getPressed()[0]:
            core.wait(0.12)
            if hover_roll:
                _commit_manual_mvc(hand_label())
                return "vertical_roll"
            if hover_mvc:
                _commit_manual_mvc(hand_label())
                return "mvc"
            if hover_stim:
                _commit_manual_mvc(hand_label())
                return "stim_session"
            if hover_stim_reset:
                reset_stim_plan_state(clear_rt=True)
                stim_rt_inputs = dict(STIM_RT_INPUT_MS)
                editing_field = None
            if hover_mvcpct:
                editing_field = "mvc"
                mvc_input_str = ""
            if hover_sa: INPUT_MODE = "arduino"; stop_labchart()
            if hover_sl: INPUT_MODE = "labchart"; init_labchart()
            if hover_hl:
                _commit_manual_mvc(hand_label())
                ACTIVE_CHANNEL = 1
            if hover_hr:
                _commit_manual_mvc(hand_label())
                ACTIVE_CHANNEL = 2
            if hover_mvc_max:
                editing_field = "mvc_max"
                manual_mvc_inputs[hand_label()]["max"] = ""
            if hover_mvc_min:
                editing_field = "mvc_min"
                manual_mvc_inputs[hand_label()]["min"] = ""
            for idx, hover_slot in enumerate(hover_mvc_slots):
                if hover_slot:
                    _commit_manual_mvc(hand_label())
                    return f"mvc_slot_{idx+1}"
            if hover_rt_near and not rt_locked:
                editing_field = "NEAR"
            if hover_rt_far and not rt_locked:
                editing_field = "FAR"
            if hover_rt_none and not rt_locked:
                editing_field = "NO TARGET"
            if hover_trig and rt_ready and not stim_plan_locked:
                _commit_manual_mvc(hand_label())
                STIM_RT_INPUT_MS = dict(stim_rt_inputs)
                STIM_SECOND_TRIGGER_ENABLED = not STIM_SECOND_TRIGGER_ENABLED
                if STIM_SECOND_TRIGGER_ENABLED:
                    generate_stim_plan()

# ---------------- INITIAL MENU ----------------
choice = show_task_menu(win)

# ---------------- Init inputs & trigger ----------------
if INPUT_MODE == "labchart":
    init_labchart()
    labchart_prepare_fro_for_choice(choice)
    labchart_start_sampling()
    settle_labchart(3.0)
else:
    init_serial()
init_trigger_serial()

# ---------------- Baseline ----------------
cue_text.text = "Center to calibrate..."; cue_text.color = "yellow"
for _ in range(10):
    check_escape_quit()
    cue_text.draw(); ball_vis.pos = (0, BASELINE_Y); ball_vis.draw(); win.flip()
session_baseline = collect_session_baseline(120)

# ---------------- MAIN LOOP ----------------
while True:
    if choice == "mvc":
        run_mvc_calibration(session_baseline)
    elif choice in ("mvc_slot_1", "mvc_slot_2", "mvc_slot_3"):
        run_mvc_calibration(session_baseline, initial_slot=int(choice[-1]) - 1)
    elif choice == "vertical_roll":
        if not have_mvc_avgs(hand_label()):
            msg = visual.TextStim(win, text="MVC averages missing for this hand.\\nComplete MVC calibration first.",
                                  color="red", height=32, pos=(0, 0))
            for _ in range(180):
                check_escape_quit()
                if ctrl_s_aborted(): break
                msg.draw(); win.flip()
        else:
            run_vertical_roll(session_baseline, trial_count=15)
    elif choice == "stim_session":
        if not have_mvc_avgs(hand_label()):
            msg = visual.TextStim(win, text="MVC averages missing for this hand.\nComplete MVC calibration first.",
                                  color="red", height=32, pos=(0, 0))
            for _ in range(180):
                check_escape_quit()
                if ctrl_s_aborted(): break
                msg.draw(); win.flip()
        elif not STIM_SECOND_TRIGGER_ENABLED or not STIM_PLAN:
            msg = visual.TextStim(win, text="Fill all RT fields, enable the 2nd trigger checkbox,\nand generate the stimulation plan first.",
                                  color="red", height=28, pos=(0, 0))
            for _ in range(180):
                check_escape_quit()
                if ctrl_s_aborted(): break
                msg.draw(); win.flip()
        elif STIM_PLAN_PROGRESS >= len(STIM_PLAN):
            msg = visual.TextStim(win, text="The 120-trial stimulation plan is complete.\nUncheck/recheck the box to generate a new plan.",
                                  color="yellow", height=28, pos=(0, 0))
            for _ in range(180):
                check_escape_quit()
                if ctrl_s_aborted(): break
                msg.draw(); win.flip()
        else:
            run_stim_session_block(session_baseline)
    else:
        break

    if INPUT_MODE == "labchart":
        labchart_stop_sampling()

    choice = show_task_menu(win)

    if INPUT_MODE == "labchart":
        init_labchart()
        labchart_prepare_fro_for_choice(choice)
        labchart_start_sampling()
        settle_labchart(3.0)
    else:
        stop_labchart()
        init_serial()

# ---------------- Cleanup ----------------
labchart_stop_sampling(); stop_labchart()
if ser:
    try: ser.close()
    except: pass
if trig_ser:
    try:
        trig_ser.write(b'\\x00'); trig_ser.close()
    except: pass
csv_file.close()
win.close()
core.quit()
