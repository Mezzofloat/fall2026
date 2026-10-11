#include <smartspectra/smartspectra.h>
#include <smartspectra/smartspectra_config.h>
#include <smartspectra/messages/metrics.h>
#include <chrono>
#include <cmath>
#include <csignal>
#include <cstdlib>
#include <iomanip>
#include <iostream>
#include <atomic>
#include <mutex>
#include <sstream>
#include <thread>

namespace sdk = presage::smartspectra;
namespace {
volatile std::sig_atomic_t stop_requested = 0;
std::mutex output_mutex;
std::atomic<int> frames_seen{0};
int env_int(const char* name, int fallback) {
    const char* v = std::getenv(name); if(!v || !*v) return fallback;
    char* end = nullptr; long n = std::strtol(v, &end, 10);
    return (end && !*end && n >= 160 && n <= 4096) ? int(n) : fallback;
}
void stop(int) { stop_requested = 1; }
std::string quote(const std::string& value) {
    std::ostringstream out; out << '"';
    for(unsigned char c : value) {
        if(c == '"' || c == '\\') out << '\\' << c;
        else if(c < 32) out << "\\u" << std::hex << std::setw(4) << std::setfill('0') << int(c);
        else out << c;
    }
    out << '"'; return out.str();
}
void emit(const std::string& event) {
    std::lock_guard lock(output_mutex); std::cout << event << std::endl;
}
void failure(const char* stage, int code) {
    // SDK error messages can contain auth/device details. Export only safe stage and code.
    emit("{\"type\":\"error\",\"message\":\"Presage " + std::string(stage) +
         " failed. Check Apple Development signing/profile, permission, key and camera.\",\"code\":" + std::to_string(code) + "}");
}
}
int main(int argc, char** argv) {
    std::signal(SIGINT, stop); std::signal(SIGTERM, stop);
    if(argc == 2 && std::string(argv[1]) == "--list-cameras") {
        std::vector<sdk::CameraInfo> cameras;
        auto result = sdk::SmartSpectra::AvailableCameras(cameras);
        if(!result.ok()) { failure("discovery", int(result.code)); return 2; }
        std::ostringstream out; out << "{\"type\":\"cameras\",\"cameras\":[";
        for(size_t i=0;i<cameras.size();++i) {
            if(i) out << ',';
            out << "{\"id\":" << quote(cameras[i].id) << ",\"name\":" << quote(cameras[i].name.value_or("Unnamed camera")) << '}';
        }
        out << "]}"; emit(out.str()); return 0;
    }
    if(argc != 3 || std::string(argv[1]) != "--camera") { failure("camera selection", -1); return 2; }
    const char* key = std::getenv("PRESAGE_API_KEY");
    if(!key || !*key) { failure("key configuration", -1); return 2; }
    sdk::SmartSpectraConfig config;
    config.api_key = key;
    config.requested_metrics = {sdk::PULSE_RATE};
    config.enable_telemetry = false;
    config.log_level = sdk::SmartSpectraLogLevel::kNone;
    sdk::SmartSpectra spectra(config);
    spectra.SetOnValidationStatusChanged([](const sdk::ValidationStatus& status, int64_t) {
        emit("{\"type\":\"validation\",\"ok\":" + std::string(status.code == sdk::ValidationCode::kOk ? "true" : "false") +
             ",\"hint\":" + quote(status.hint.substr(0,200)) + "}");
    });
    spectra.SetOnMetrics([](const sdk::Metrics& metrics, int64_t) {
        if(!metrics.has_cardio()) return;
        for(const auto& reading : metrics.cardio().pulse_rate()) {
            if(!std::isfinite(reading.value()) || !std::isfinite(reading.confidence())) continue;
            std::ostringstream out; out << std::setprecision(12)
                << "{\"type\":\"pulse\",\"bpm\":" << reading.value()
                << ",\"confidence\":" << reading.confidence()
                << ",\"stable\":" << (reading.stable()?"true":"false")
                << ",\"timestamp_us\":" << reading.timestamp() << '}';
            emit(out.str());
        }
    });
    spectra.SetOnError([](const sdk::SmartSpectraError& error) { failure("session", int(error.code)); stop_requested = 1; });
    spectra.SetOnFrameSentThrough([](bool sent, int64_t) { if(sent) ++frames_seen; });
    const int width = env_int("PRESAGE_WIDTH", 1280), height = env_int("PRESAGE_HEIGHT", 720);
    int fps = env_int("PRESAGE_FPS", 30); if(fps > 120) fps = 30;
    auto source = spectra.UseCamera(sdk::CameraSelection::ById(argv[2])).SetResolution(width,height).SetFps(fps).Build();
    if(!source.ok()) { failure("camera configuration", int(source.code)); return 3; }
    auto started = spectra.Start();
    if(!started.ok()) { failure("startup", int(started.code)); return 4; }
    emit("{\"type\":\"started\"}");
    auto window = std::chrono::steady_clock::now();
    while(!stop_requested) {
        std::this_thread::sleep_for(std::chrono::milliseconds(100));
        auto now = std::chrono::steady_clock::now();
        double secs = std::chrono::duration<double>(now - window).count();
        if(secs >= 2.0) {
            int n = frames_seen.exchange(0); window = now;
            std::ostringstream out; out << "{\"type\":\"fps\",\"fps\":" << std::fixed << std::setprecision(1) << n / secs
                << ",\"width\":" << width << ",\"height\":" << height << ",\"requested_fps\":" << fps << '}';
            emit(out.str());
        }
    }
    auto stopped = spectra.Stop();
    if(!stopped.ok()) failure("stop", int(stopped.code));
    return 0;
}
