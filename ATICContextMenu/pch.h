#ifndef PCH_H
#define PCH_H

#include "framework.h"

#include <filesystem>
#include <string>

#include <shlobj_core.h>
#include <shlwapi.h>
#pragma comment(lib, "shlwapi.lib")

#include <wrl/client.h>
#include <wrl/implements.h>
#include <wrl/module.h>
#pragma comment(lib, "runtimeobject.lib")

#pragma warning(push)
#pragma warning(disable: 28182)
#include <wil/stl.h>
#include <wil/win32_helpers.h>
#pragma warning(pop)

#endif
