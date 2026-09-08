const SCAN_MODAL_ID = 'faceScanModal';
const VIDEO_ID = 'camera';
const GUIDE_ID = 'faceGuide';
const RING_ID = 'faceRing';
const STATUS_ID = 'scanStatus';

const DETECTOR_INPUT_SIZE = 416;
const DETECTOR_SCORE_THRESHOLD = 0.2;

const CAPTURE_WIDTH = 480;
const CAPTURE_HEIGHT = 360;
const JPEG_QUALITY = 0.92;
const CAMERA_WARM_UP_MS = 600;
const DETECT_INTERVAL_MS = 200;
const RING_TIMEOUT_MS = 45000;
const CLOSE_UP_TIMEOUT_MS = 20000;

const RING_SEGMENT_COUNT = 8;
const RING_DETECT_INTERVAL_MS = 100;
const RING_INNER_RADIUS = 40;
const RING_OUTER_RADIUS = 46;
const YAW_FRONTAL_RATE = 0.50;
const PITCH_FRONTAL_RATE = 0.45;
const YAW_TURN_SPAN = 0.12;
const PITCH_TURN_SPAN = 0.11;
const RING_TURN_THRESHOLD = 0.45;

const MIN_OVAL_FILL_RATE = 0.80;
const MAX_OVAL_FILL_RATE = 1.10;
const MAX_OVAL_OFFSET_RATE = 0.15;
const STABLE_FRAME_COUNT = 3;

const faceScanner = {
    stream: null,
    detectorOptions: null,
    modelLoaded: false,
    cancelled: false,

    async loadDetector() {
        if (this.modelLoaded) return;
        await faceapi.nets.tinyFaceDetector.loadFromUri('/models');
        await faceapi.nets.faceLandmark68TinyNet.loadFromUri('/models');
        this.detectorOptions = new faceapi.TinyFaceDetectorOptions({
            inputSize: DETECTOR_INPUT_SIZE,
            scoreThreshold: DETECTOR_SCORE_THRESHOLD
        });
        this.modelLoaded = true;
    },

    async startCamera(video) {
        if (this.stream) return;
        this.stream = await navigator.mediaDevices.getUserMedia({
            video: { width: CAPTURE_WIDTH, height: CAPTURE_HEIGHT }
        });
        video.srcObject = this.stream;
        await video.play();
        await this.waitForFrame(video);
    },

    waitForFrame(video) {
        return new Promise(resolve => {
            const check = () => {
                if (video.readyState >= 2 && video.videoWidth > 0) resolve();
                else setTimeout(check, 100);
            };
            check();
        });
    },

    stopCamera() {
        if (!this.stream) return;
        this.stream.getTracks().forEach(track => track.stop());
        this.stream = null;
    },

    sleep(milliseconds) {
        return new Promise(resolve => setTimeout(resolve, milliseconds));
    },

    buildRing(ring) {
        ring.replaceChildren();

        for (let index = 0; index < RING_SEGMENT_COUNT; index++) {
            const angle = index * (2 * Math.PI / RING_SEGMENT_COUNT);
            const line = document.createElementNS('http://www.w3.org/2000/svg', 'line');

            line.setAttribute('x1', 50 + RING_INNER_RADIUS * Math.cos(angle));
            line.setAttribute('y1', 50 + RING_INNER_RADIUS * Math.sin(angle));
            line.setAttribute('x2', 50 + RING_OUTER_RADIUS * Math.cos(angle));
            line.setAttribute('y2', 50 + RING_OUTER_RADIUS * Math.sin(angle));

            ring.appendChild(line);
        }
    },

    contentRect(video) {
        const box = video.getBoundingClientRect();
        const frameAspect = video.videoWidth / video.videoHeight;
        const boxAspect = box.width / box.height;

        const width = frameAspect > boxAspect ? box.height * frameAspect : box.width;
        const height = frameAspect > boxAspect ? box.height : box.width / frameAspect;

        return {
            left: box.left + (box.width - width) / 2,
            top: box.top + (box.height - height) / 2,
            scale: width / video.videoWidth
        };
    },

    readOval(video, guide) {
        const content = this.contentRect(video);
        const box = guide.getBoundingClientRect();

        return {
            centerX: (box.left + box.width / 2 - content.left) / content.scale,
            centerY: (box.top + box.height / 2 - content.top) / content.scale,
            width: box.width / content.scale,
            height: box.height / content.scale
        };
    },

    detectFace(video) {
        return faceapi.detectSingleFace(video, this.detectorOptions).withFaceLandmarks(true);
    },

    // Mũi nằm đâu trong bề ngang hàm cho biết mặt xoay trái hay phải,
    // nằm đâu giữa đường mắt và cằm cho biết mặt ngẩng hay cúi
    readPose(detection) {
        const points = detection.landmarks.positions;
        const jawLeft = points[0];
        const jawRight = points[16];
        const noseTip = points[30];
        const chin = points[8];
        const leftEye = points[36];
        const rightEye = points[45];

        const jawWidth = jawRight.x - jawLeft.x;
        const eyeLineY = (leftEye.y + rightEye.y) / 2;
        const faceHeight = chin.y - eyeLineY;

        return {
            yawRate: jawWidth === 0 ? YAW_FRONTAL_RATE : (noseTip.x - jawLeft.x) / jawWidth,
            pitchRate: faceHeight === 0 ? PITCH_FRONTAL_RATE : (noseTip.y - eyeLineY) / faceHeight
        };
    },

    // Đổi hướng mặt thành một điểm trên vòng tròn: quay mạnh tới đâu, về phía nào
    readTurnDirection(pose) {
        const yawOffset = (pose.yawRate - YAW_FRONTAL_RATE) / YAW_TURN_SPAN;
        const pitchOffset = (pose.pitchRate - PITCH_FRONTAL_RATE) / PITCH_TURN_SPAN;

        return {
            strength: Math.hypot(yawOffset, pitchOffset),
            segment: this.segmentAt(Math.atan2(pitchOffset, yawOffset))
        };
    },

    segmentAt(angle) {
        const step = 2 * Math.PI / RING_SEGMENT_COUNT;
        return (Math.round(angle / step) + RING_SEGMENT_COUNT) % RING_SEGMENT_COUNT;
    },

    fillSegment(ring, filled, segment) {
        if (filled.has(segment)) return;
        filled.add(segment);
        ring.children[segment].classList.add('done');
    },

    // Hai lần đo cách nhau 100ms, quay nhanh là nhảy qua mấy vạch giữa,
    // nên tô luôn cả quãng ngắn nối hai vạch thay vì chỉ tô vạch vừa đo được
    fillSegmentsBetween(ring, filled, fromSegment, toSegment) {
        if (fromSegment === null) {
            this.fillSegment(ring, filled, toSegment);
            return;
        }

        const forward = (toSegment - fromSegment + RING_SEGMENT_COUNT) % RING_SEGMENT_COUNT;
        const backward = RING_SEGMENT_COUNT - forward;
        const stepCount = Math.min(forward, backward);
        const direction = forward <= backward ? 1 : -1;

        for (let step = 0; step <= stepCount; step++) {
            const segment = (fromSegment + direction * step + RING_SEGMENT_COUNT * 2) % RING_SEGMENT_COUNT;
            this.fillSegment(ring, filled, segment);
        }
    },

    async fillRingByTurningHead(video, ring, onProgress) {
        const startTime = Date.now();
        const filled = new Set();
        let previousSegment = null;

        while (Date.now() - startTime < RING_TIMEOUT_MS) {
            if (this.cancelled) return false;

            await this.sleep(RING_DETECT_INTERVAL_MS);
            const detection = await this.detectFace(video);

            if (!detection) {
                previousSegment = null;
                onProgress('No face detected, look at the camera');
                continue;
            }

            const turn = this.readTurnDirection(this.readPose(detection));
            console.log('[face] score', detection.detection.score.toFixed(2),
                        'strength', turn.strength.toFixed(2), 'segment', turn.segment,
                        'filled', filled.size + '/' + RING_SEGMENT_COUNT);

            // Về giữa thì coi như hết một lượt quay, lần sau tô lại từ đầu chứ không nối quãng
            if (turn.strength < RING_TURN_THRESHOLD) {
                previousSegment = null;
                onProgress(`Slowly move your head in a circle... ${filled.size}/${RING_SEGMENT_COUNT}`);
                continue;
            }

            this.fillSegmentsBetween(ring, filled, previousSegment, turn.segment);
            previousSegment = turn.segment;

            if (filled.size >= RING_SEGMENT_COUNT) return true;

            onProgress(`Keep going... ${filled.size}/${RING_SEGMENT_COUNT}`);
        }

        return false;
    },

    async measureFaceAgainstOval(video, guide) {
        const detection = await this.detectFace(video);
        if (!detection) return { found: false };

        const oval = this.readOval(video, guide);
        const box = detection.detection.box;

        return {
            found: true,
            fillRate: box.width / oval.width,
            offsetXRate: Math.abs(box.x + box.width / 2 - oval.centerX) / oval.width,
            offsetYRate: Math.abs(box.y + box.height / 2 - oval.centerY) / oval.height
        };
    },

    async waitUntilFaceFillsOval(video, guide, onProgress) {
        const startTime = Date.now();
        let goodFrames = 0;

        while (Date.now() - startTime < CLOSE_UP_TIMEOUT_MS) {
            if (this.cancelled) return false;

            await this.sleep(DETECT_INTERVAL_MS);
            const face = await this.measureFaceAgainstOval(video, guide);

            if (!face.found) {
                goodFrames = 0;
                onProgress('No face detected, move into the green oval');
                continue;
            }

            console.log('[face] fill', face.fillRate.toFixed(2),
                        'offsetX', face.offsetXRate.toFixed(2),
                        'offsetY', face.offsetYRate.toFixed(2));

            if (face.offsetXRate > MAX_OVAL_OFFSET_RATE || face.offsetYRate > MAX_OVAL_OFFSET_RATE) {
                goodFrames = 0;
                onProgress('Center your face inside the green oval');
                continue;
            }

            if (face.fillRate < MIN_OVAL_FILL_RATE) {
                goodFrames = 0;
                onProgress('Move closer until your face fills the green oval');
                continue;
            }

            if (face.fillRate > MAX_OVAL_FILL_RATE) {
                goodFrames = 0;
                onProgress('Move back a little, your face is larger than the oval');
                continue;
            }

            goodFrames++;
            if (goodFrames >= STABLE_FRAME_COUNT) return true;

            onProgress('Hold still...');
        }

        return false;
    },

    capturePhoto(video) {
        if (video.videoWidth === 0 || video.videoHeight === 0) return null;

        const canvas = document.createElement('canvas');
        canvas.width = video.videoWidth;
        canvas.height = video.videoHeight;
        canvas.getContext('2d').drawImage(video, 0, 0, canvas.width, canvas.height);

        return canvas.toDataURL('image/jpeg', JPEG_QUALITY);
    }
};

function setupFaceCapture(options) {
    const modalElement = document.getElementById(SCAN_MODAL_ID);
    const video = document.getElementById(VIDEO_ID);
    const guide = document.getElementById(GUIDE_ID);
    const ring = document.getElementById(RING_ID);
    const status = document.getElementById(STATUS_ID);
    const result = document.getElementById(options.resultId);
    const imageInput = document.getElementById(options.imageId);
    const scanButton = document.getElementById(options.scanButtonId);
    const submitButton = document.getElementById(options.submitButtonId);
    const isOptional = options.optional === true;
    const needRing = options.ring === true;

    const modal = new bootstrap.Modal(modalElement);
    submitButton.disabled = !isOptional;

    // Đóng modal giữa chừng thì phải tắt camera và cho vòng lặp đang chạy dừng lại
    modalElement.addEventListener('hidden.bs.modal', () => {
        faceScanner.cancelled = true;
        faceScanner.stopCamera();
    });

    function openModal() {
        return new Promise(resolve => {
            modalElement.addEventListener('shown.bs.modal', resolve, { once: true });
            modal.show();
        });
    }

    async function runScan(showHint) {
        showHint('Loading face model...');
        await faceScanner.loadDetector();

        showHint('Starting the camera...');
        await faceScanner.startCamera(video);
        await faceScanner.sleep(CAMERA_WARM_UP_MS);

        if (needRing) {
            const ringFilled = await faceScanner.fillRingByTurningHead(video, ring, showHint);
            if (!ringFilled) return { ok: false, message: 'Could not finish the head turn, please scan again.' };

            showHint('Great, now move closer until your face fills the green oval');
        }

        const isReady = await faceScanner.waitUntilFaceFillsOval(video, guide, showHint);
        if (!isReady) return { ok: false, message: 'Cannot see your face clearly, please scan again.' };

        const photo = faceScanner.capturePhoto(video);
        if (!photo) return { ok: false, message: 'Cannot take your photo, please scan again.' };

        return { ok: true, photo };
    }

    scanButton.addEventListener('click', async () => {
        scanButton.disabled = true;
        submitButton.disabled = !isOptional;
        imageInput.value = '';

        faceScanner.cancelled = false;
        faceScanner.buildRing(ring);

        const showHint = hint => status.textContent = hint;
        await openModal();

        try {
            const scan = await runScan(showHint);

            if (!scan.ok) {
                result.textContent = faceScanner.cancelled ? 'Face scan was cancelled' : scan.message;
                modal.hide();
                return;
            }

            imageInput.value = scan.photo;
            submitButton.disabled = false;
            result.textContent = 'Photo taken, you can continue';
            modal.hide();

            if (options.autoSubmit) {
                result.textContent = 'Photo taken, signing you in...';
                setTimeout(() => submitButton.click(), 600);
            }
        } catch (error) {
            result.textContent = 'Camera error: ' + error.message;
            modal.hide();
        } finally {
            faceScanner.stopCamera();
            scanButton.disabled = false;
        }
    });
}
