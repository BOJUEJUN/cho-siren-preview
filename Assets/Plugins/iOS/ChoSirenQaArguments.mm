#import <Foundation/Foundation.h>
#include <limits.h>
#include <stdlib.h>
#include <string.h>

// Read launch arguments only. Each non-null result is a caller-owned UTF-8 copy;
// it survives the autorelease pool and must be freed with ChoSirenQaFreeArgument.
extern "C" char *ChoSirenQaCopyArgument(const char *utf8Name, int *byteCount)
{
    if (byteCount != NULL) *byteCount = 0;
    if (utf8Name == NULL) return NULL;
    @autoreleasepool
    {
        NSString *name = [NSString stringWithUTF8String:utf8Name];
        if (name == nil) return NULL;
        BOOL known = [name caseInsensitiveCompare:@"-smokeCapture"] == NSOrderedSame ||
            [name caseInsensitiveCompare:@"-smokeScreen"] == NSOrderedSame ||
            [name caseInsensitiveCompare:@"-smokeDelay"] == NSOrderedSame ||
            [name caseInsensitiveCompare:@"-smokeLevel"] == NSOrderedSame;
        if (!known) return NULL;
        NSArray<NSString *> *arguments = [NSProcessInfo processInfo].arguments;
        for (NSUInteger index = 0; index + 1 < arguments.count; ++index)
        {
            if ([arguments[index] caseInsensitiveCompare:name] != NSOrderedSame) continue;
            const char *value = arguments[index + 1].UTF8String;
            if (value == NULL) return NULL;
            size_t length = strlen(value);
            if (length > INT_MAX) return NULL;
            char *copy = static_cast<char *>(malloc(length + 1));
            if (copy == NULL) return NULL;
            memcpy(copy, value, length + 1);
            if (byteCount != NULL) *byteCount = static_cast<int>(length);
            return copy;
        }
        return NULL;
    }
}

extern "C" void ChoSirenQaFreeArgument(void *value)
{
    free(value); // free(NULL) is intentional and safe.
}
