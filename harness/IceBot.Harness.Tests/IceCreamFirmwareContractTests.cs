using System;
using System.IO;
using Xunit;

namespace IceBot.Harness.Tests
{
    public class IceCreamFirmwareContractTests
    {
        private static string FirmwareRoot => Path.GetFullPath(Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\firmware\ice-cream-controller"));

        private static string Read(string relativePath) =>
            File.ReadAllText(Path.Combine(FirmwareRoot, relativePath));

        [Fact]
        public void Firmware_MapsCommandsToMeasuredPhysicalDirections()
        {
            var source = Read(@"Core\Src\main.c");
            var up = Between(source, "static uint8_t Motor_RunUp", "static uint8_t Motor_RunDown");
            var down = Between(source, "static uint8_t Motor_RunDown", "static uint8_t UpperLimit_IsActive");

            Assert.Contains("speedPercent = 20;", up);
            Assert.Contains("speedPercent = 20;", down);
            Assert.Contains("Motor_SetPwm(&htim1, TIM_CHANNEL_1, 0);", up);
            Assert.Contains("Motor_SetPwm(&htim3, TIM_CHANNEL_4, speedPercent);", up);
            Assert.Contains("Motor_SetPwm(&htim3, TIM_CHANNEL_4, 0);", down);
            Assert.Contains("Motor_SetPwm(&htim1, TIM_CHANNEL_1, speedPercent);", down);
        }

        [Fact]
        public void Firmware_ConfiguresUpperLimitAsActiveLowExtiInput()
        {
            var main = Read(@"Core\Src\main.c");
            var header = Read(@"Core\Inc\main.h");
            var interrupts = Read(@"Core\Src\stm32f1xx_it.c");

            Assert.Contains("UPPER_LIMIT_SWITCH_Pin GPIO_PIN_0", header);
            Assert.Contains("LOWER_LIMIT_SWITCH_Pin GPIO_PIN_10", header);
            Assert.Contains("GPIO_MODE_IT_RISING_FALLING", main);
            Assert.Contains("GPIO_PULLUP", main);
            Assert.Contains("GPIO_InitStruct.Pin = LOWER_LIMIT_SWITCH_Pin;", main);
            Assert.Contains("HAL_GPIO_Init(LOWER_LIMIT_SWITCH_GPIO_Port, &GPIO_InitStruct);", main);
            Assert.Contains("HAL_NVIC_EnableIRQ(EXTI15_10_IRQn);", main);
            Assert.Contains("void EXTI0_IRQHandler", interrupts);
            Assert.Contains("HAL_GPIO_EXTI_IRQHandler(UPPER_LIMIT_SWITCH_Pin)", interrupts);
            Assert.Contains("HAL_GPIO_EXTI_IRQHandler(LOWER_LIMIT_SWITCH_Pin)", interrupts);
        }

        [Fact]
        public void Firmware_StopsAndRejectsPhysicalUpAtUpperLimitButKeepsDownAvailable()
        {
            var source = Read(@"Core\Src\main.c");
            var up = Between(source, "static uint8_t Motor_RunUp", "static uint8_t Motor_RunDown");
            var poll = BetweenDefinitions(source, "static void Motor_Poll(void)", "static void Protocol_Poll(void)");

            Assert.Contains("if (UpperLimit_IsActive())", up);
            Assert.Contains("return 0; // physical upper limit is active", up);
            Assert.Contains("upperLimitActive && currentDir == MOTOR_UP", poll);
            Assert.Contains("static uint8_t Motor_RunDown", source);
            Assert.Contains("physical DOWN", source);
            Assert.Contains("LowerLimit_IsActive()", source);
        }

        [Fact]
        public void Firmware_HandlesBothLimitInterruptsAndStopsOnlyMatchingDirection()
        {
            var callback = Between(Read(@"Core\Src\main.c"),
                "void HAL_GPIO_EXTI_Callback", "void HAL_UART_RxCpltCallback");

            Assert.Contains("GPIO_Pin == UPPER_LIMIT_SWITCH_Pin || GPIO_Pin == LOWER_LIMIT_SWITCH_Pin", callback);
            Assert.Contains("upperLimitActive = UpperLimit_IsActive();", callback);
            Assert.Contains("lowerLimitActive = LowerLimit_IsActive();", callback);
            Assert.Contains("(upperLimitActive && currentDir == MOTOR_UP) || (lowerLimitActive && currentDir == MOTOR_DOWN)", callback);
            Assert.Contains("Motor_Stop(upperLimitActive ? MOTOR_STOP_CAUSE_UPPER_LIMIT : MOTOR_STOP_CAUSE_LOWER_LIMIT);", callback);
        }

        [Fact]
        public void Firmware_ExposesReleaseDebuggerDiagnosticsForLimitsStopsAndResetCause()
        {
            var source = Read(@"Core\Src\main.c");

            Assert.Contains("volatile uint32_t icebot_diag_boot_reset_flags_raw", source);
            Assert.Contains("icebot_diag_boot_reset_flags_raw = RCC->CSR;", source);
            Assert.Contains("RCC->CSR |= RCC_CSR_RMVF;", source);
            Assert.Contains("volatile uint32_t icebot_diag_uptime_ms", source);
            Assert.Contains("volatile uint32_t icebot_diag_upper_active_event_count", source);
            Assert.Contains("volatile uint32_t icebot_diag_lower_active_event_count", source);
            Assert.Contains("volatile IceBotDiagStopEvent icebot_diag_stop_events", source);
            Assert.Contains("MOTOR_STOP_CAUSE_UPPER_LIMIT", source);
            Assert.Contains("MOTOR_STOP_CAUSE_LOWER_LIMIT", source);
            Assert.Contains("MOTOR_STOP_CAUSE_DEADLINE", source);
            Assert.Contains("Diagnostics_RecordLimitActive(GPIO_Pin, observedUpperActive, observedLowerActive);", source);
            Assert.Contains("event->gpioSnapshot = Diagnostics_ReadGpioSnapshot();", source);
            Assert.Contains("event->sequence = sequence + 1U;", source);
        }

        [Fact]
        public void Firmware_UsesCauseSpecificStopCallsAtAllSafetyAndCommandPaths()
        {
            var source = Read(@"Core\Src\main.c");
            var callback = Between(source, "void HAL_GPIO_EXTI_Callback", "void HAL_UART_RxCpltCallback");
            var poll = BetweenDefinitions(source, "static void Motor_Poll(void)", "static void Protocol_Poll(void)");

            Assert.Contains("Motor_Stop(MOTOR_STOP_CAUSE_COMMAND)", source);
            Assert.Contains("Motor_Stop(MOTOR_STOP_CAUSE_BOOT)", source);
            Assert.Contains("Motor_Stop(MOTOR_STOP_CAUSE_DEADLINE)", poll);
            Assert.Contains("MOTOR_STOP_CAUSE_UPPER_LIMIT", callback);
            Assert.Contains("MOTOR_STOP_CAUSE_LOWER_LIMIT", callback);
            Assert.DoesNotContain("Motor_Stop();", source);
        }

        [Fact]
        public void FirmwareStopsPwmBeforePublishingStopDiagnosticsAndPreservesDirection()
        {
            var source = Read(@"Core\Src\main.c");
            var stop = BetweenDefinitions(source, "static uint8_t Motor_Stop(MotorStopCause cause)", "// speedPercent 0 = use MCU default");

            Assert.Contains("__disable_irq();", stop);
            Assert.Contains("direction = currentDir;", stop);
            Assert.True(stop.IndexOf("Motor_SetPwm(&htim1", StringComparison.Ordinal) <
                        stop.IndexOf("Diagnostics_RecordStop(cause, direction", StringComparison.Ordinal));
            Assert.Contains("volatile IceBotDiagStopEvent *event;", source);
            Assert.DoesNotContain("(IceBotDiagStopEvent *)&icebot_diag_stop_events", source);
        }

        [Fact]
        public void FirmwareUsesOneLimitSnapshotAndMatchingDirectionForExtiCause()
        {
            var source = Read(@"Core\Src\main.c");
            var callback = BetweenDefinitions(source, "void HAL_GPIO_EXTI_Callback(uint16_t GPIO_Pin)", "void HAL_UART_RxCpltCallback");

            Assert.Contains("uint8_t observedUpperActive = UpperLimit_IsActive();", callback);
            Assert.Contains("uint8_t observedLowerActive = LowerLimit_IsActive();", callback);
            Assert.Contains("currentDir == MOTOR_UP && observedUpperActive", callback);
            Assert.Contains("currentDir == MOTOR_DOWN && observedLowerActive", callback);
            Assert.Contains("Diagnostics_RecordLimitActive(GPIO_Pin, observedUpperActive, observedLowerActive);", callback);
            Assert.DoesNotContain("upperLimitActive ? MOTOR_STOP_CAUSE_UPPER_LIMIT : MOTOR_STOP_CAUSE_LOWER_LIMIT", callback);
        }

        [Fact]
        public void Firmware_RejectsMatchingLimitButAllowsOppositeDirection()
        {
            var source = Read(@"Core\Src\main.c");
            var up = Between(source, "static uint8_t Motor_RunUp", "static uint8_t Motor_RunDown");
            var down = Between(source, "static uint8_t Motor_RunDown", "static uint8_t UpperLimit_IsActive");

            Assert.Contains("if (UpperLimit_IsActive())", up);
            Assert.DoesNotContain("LowerLimit_IsActive()", up);
            Assert.Contains("if (LowerLimit_IsActive())", down);
            Assert.Contains("return 0; // physical lower limit is active", down);
            Assert.DoesNotContain("UpperLimit_IsActive()", down);
        }

        private static string Between(string source, string start, string end)
        {
            var startIndex = source.LastIndexOf(start, StringComparison.Ordinal);
            var endIndex = source.LastIndexOf(end, StringComparison.Ordinal);
            Assert.True(startIndex >= 0, "Missing start marker: " + start);
            Assert.True(endIndex > startIndex, "Missing end marker: " + end);
            return source.Substring(startIndex, endIndex - startIndex);
        }

        private static string BetweenDefinitions(string source, string start, string end)
        {
            var startIndex = FindDefinition(source, start);
            var endIndex = source.LastIndexOf(end, StringComparison.Ordinal);
            Assert.True(startIndex >= 0, "Missing definition: " + start);
            Assert.True(endIndex > startIndex, "Missing end marker: " + end);
            return source.Substring(startIndex, endIndex - startIndex);
        }

        private static int FindDefinition(string source, string signature)
        {
            var searchFrom = 0;
            while (searchFrom < source.Length)
            {
                var signatureIndex = source.IndexOf(signature, searchFrom, StringComparison.Ordinal);
                if (signatureIndex < 0)
                {
                    return -1;
                }

                var braceIndex = source.IndexOf('{', signatureIndex);
                var semicolonIndex = source.IndexOf(';', signatureIndex);
                if (braceIndex >= 0 && (semicolonIndex < 0 || braceIndex < semicolonIndex))
                {
                    return signatureIndex;
                }

                searchFrom = signatureIndex + signature.Length;
            }

            return -1;
        }
    }
}
