#include "pch.h"
#include "resource.h"

using namespace Microsoft::WRL;

static constexpr wchar_t exe_filename[] = L"AT Image Converter.exe";

// {AB4DC84E-A904-401F-9A3D-3F55D3770D25}
static constexpr CLSID CLSID_ATICContextMenu =
{ 0xAB4DC84E, 0xA904, 0x401F, { 0x9A, 0x3D, 0x3F, 0x55, 0xD3, 0x77, 0x0D, 0x25 } };

std::wstring GetModuleDirectoryPath()
{
    std::wstring path(MAX_PATH, L'\0');
    DWORD length = 0;
    for (;;)
    {
        length = GetModuleFileNameW(wil::GetModuleInstanceHandle(), path.data(), static_cast<DWORD>(path.size()));
        if (length == 0)
            return {};
        if (length < static_cast<DWORD>(path.size()))
            break;
        path.resize(path.size() * 2);
    }
    path.resize(length);
    auto lastSlash = path.find_last_of(L'\\');
    if (lastSlash != std::wstring::npos)
        path.resize(lastSlash + 1);
    return path;
}

std::wstring QuoteForCommandLineArgument(const std::wstring& argument)
{
    // If the argument is empty, return a pair of quotes
    if (argument.empty())
        return L"\"\"";

    // Check if quoting is needed
    bool needsQuoting = false;
    for (auto character : argument)
    {
        if (character == L' ' || character == L'\t' || character == L'"' || character == L'\\')
        {
            needsQuoting = true;
            break;
        }
    }

    if (!needsQuoting)
        return argument;

    std::wstring quoted = L"\"";
    for (auto iterator = argument.begin(); ; ++iterator)
    {
        size_t backslashCount = 0;
        while (iterator != argument.end() && *iterator == L'\\')
        {
            ++iterator;
            ++backslashCount;
        }

        if (iterator == argument.end())
        {
            // Escape all backslashes before the closing quote
            quoted.append(backslashCount * 2, L'\\');
            break;
        }
        else if (*iterator == L'"')
        {
            // Escape backslashes and the quote
            quoted.append(backslashCount * 2 + 1, L'\\');
            quoted.push_back(*iterator);
        }
        else
        {
            // Backslashes are literal here
            quoted.append(backslashCount, L'\\');
            quoted.push_back(*iterator);
        }
    }
    quoted.push_back(L'"');
    return quoted;
}

class __declspec(uuid("AB4DC84E-A904-401F-9A3D-3F55D3770D25"))
ATICContextMenuCommand : public RuntimeClass<
    RuntimeClassFlags<ClassicCom>,
    IExplorerCommand>
{
public:
    // IExplorerCommand
    IFACEMETHODIMP GetTitle(_In_opt_ IShellItemArray* /*items*/, _Outptr_result_nullonfailure_ PWSTR* name)
    {
        *name = nullptr;
        wchar_t buffer[256] = {};
        if (LoadStringW(wil::GetModuleInstanceHandle(), IDS_MENU_TITLE, buffer, ARRAYSIZE(buffer)) > 0)
            return SHStrDupW(buffer, name);
        return SHStrDupW(L"Convert with AT Image Converter", name);
    }

    IFACEMETHODIMP GetIcon(_In_opt_ IShellItemArray* /*items*/, _Outptr_result_nullonfailure_ PWSTR* icon)
    {
        *icon = nullptr;
        std::wstring iconPath = GetModuleDirectoryPath() + exe_filename;
        return SHStrDupW(iconPath.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(_In_opt_ IShellItemArray* /*items*/, _Outptr_result_nullonfailure_ PWSTR* infoTip)
    {
        *infoTip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(_Out_ GUID* guidCommandName)
    {
        *guidCommandName = CLSID_ATICContextMenu;
        return S_OK;
    }

    IFACEMETHODIMP GetState(_In_opt_ IShellItemArray* /*items*/, _In_ BOOL /*okToBeSlow*/, _Out_ EXPCMDSTATE* commandState)
    {
        *commandState = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP GetFlags(_Out_ EXPCMDFLAGS* flags)
    {
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(_COM_Outptr_ IEnumExplorerCommand** enumCommands)
    {
        *enumCommands = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP Invoke(_In_opt_ IShellItemArray* items, _In_opt_ IBindCtx* /*bindCtx*/) noexcept
    try
    {
        if (!items)
            return S_OK;

        DWORD itemCount = 0;
        RETURN_IF_FAILED(items->GetCount(&itemCount));
        if (itemCount == 0)
            return S_OK;

        std::wstring moduleDirectory = GetModuleDirectoryPath();
        std::wstring executablePath = moduleDirectory + exe_filename;

        // Write file paths to a temporary file to avoid command line length limit
        wchar_t tempDir[MAX_PATH] = {};
        GetTempPathW(MAX_PATH, tempDir);

        wchar_t tempFilePath[MAX_PATH] = {};
        GetTempFileNameW(tempDir, L"ATIC", 0, tempFilePath);

        HANDLE hFile = CreateFileW(tempFilePath, GENERIC_WRITE, 0, nullptr, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, nullptr);
        if (hFile == INVALID_HANDLE_VALUE)
            return HRESULT_FROM_WIN32(GetLastError());

        // Write BOM for UTF-8
        const unsigned char bom[] = { 0xEF, 0xBB, 0xBF };
        DWORD bytesWritten = 0;
        WriteFile(hFile, bom, sizeof(bom), &bytesWritten, nullptr);

        for (DWORD i = 0; i < itemCount; ++i)
        {
            ComPtr<IShellItem> shellItem;
            RETURN_IF_FAILED(items->GetItemAt(i, &shellItem));

            wil::unique_cotaskmem_string filePath;
            RETURN_IF_FAILED(shellItem->GetDisplayName(SIGDN_FILESYSPATH, &filePath));

            // Convert wide string to UTF-8
            int utf8Length = WideCharToMultiByte(CP_UTF8, 0, filePath.get(), -1, nullptr, 0, nullptr, nullptr);
            if (utf8Length > 0)
            {
                std::string utf8Path(utf8Length - 1, '\0');
                WideCharToMultiByte(CP_UTF8, 0, filePath.get(), -1, utf8Path.data(), utf8Length, nullptr, nullptr);
                utf8Path += '\n';
                WriteFile(hFile, utf8Path.c_str(), static_cast<DWORD>(utf8Path.size()), &bytesWritten, nullptr);
            }
        }

        CloseHandle(hFile);

        // Build command line: "exe_path" --file-list "tempfile"
        std::wstring commandLine = QuoteForCommandLineArgument(executablePath);
        commandLine += L" --file-list ";
        commandLine += QuoteForCommandLineArgument(tempFilePath);

        STARTUPINFOW startupInfo = { sizeof(startupInfo) };
        PROCESS_INFORMATION processInfo = {};

        if (CreateProcessW(
            executablePath.c_str(),
            commandLine.data(),
            nullptr,
            nullptr,
            FALSE,
            0,
            nullptr,
            moduleDirectory.c_str(),
            &startupInfo,
            &processInfo))
        {
            CloseHandle(processInfo.hThread);
            CloseHandle(processInfo.hProcess);
        }
        else
        {
            // Clean up temp file if process creation fails
            DeleteFileW(tempFilePath);
        }

        return S_OK;
    }
    CATCH_RETURN()
};

CoCreatableClass(ATICContextMenuCommand)
CoCreatableClassWrlCreatorMapInclude(ATICContextMenuCommand)

STDAPI DllGetClassObject(_In_ REFCLSID rclsid, _In_ REFIID riid, _COM_Outptr_ void** instance)
{
    return Module<InProc>::GetModule().GetClassObject(rclsid, riid, instance);
}

STDAPI DllCanUnloadNow()
{
    return Module<InProc>::GetModule().GetObjectCount() == 0 ? S_OK : S_FALSE;
}

BOOL APIENTRY DllMain(HMODULE /*hModule*/, DWORD reason, LPVOID /*lpReserved*/)
{
    switch (reason)
    {
    case DLL_PROCESS_ATTACH:
    case DLL_THREAD_ATTACH:
    case DLL_THREAD_DETACH:
    case DLL_PROCESS_DETACH:
        break;
    }
    return TRUE;
}
