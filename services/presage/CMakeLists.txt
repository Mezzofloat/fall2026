cmake_minimum_required(VERSION 3.22)
project(BreathePresage LANGUAGES CXX)
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
find_package(SmartSpectra 3.4 CONFIG REQUIRED)
add_executable(breathe_presage MACOSX_BUNDLE main.cpp)
target_link_libraries(breathe_presage PRIVATE SmartSpectra::SDK)
set_target_properties(breathe_presage PROPERTIES
    MACOSX_BUNDLE_BUNDLE_NAME BreathePresage
    MACOSX_BUNDLE_GUI_IDENTIFIER com.breatheexe.presage
    MACOSX_BUNDLE_INFO_PLIST "${CMAKE_CURRENT_SOURCE_DIR}/Info.plist.in"
    OUTPUT_NAME breathe_presage)
# Keep the bundle's visible name distinct from its executable.
set_target_properties(breathe_presage PROPERTIES MACOSX_BUNDLE_BUNDLE_NAME BreathePresage)
add_custom_command(TARGET breathe_presage POST_BUILD
    COMMAND ${CMAKE_COMMAND} -E create_symlink "breathe_presage.app" "${CMAKE_BINARY_DIR}/BreathePresage.app")
