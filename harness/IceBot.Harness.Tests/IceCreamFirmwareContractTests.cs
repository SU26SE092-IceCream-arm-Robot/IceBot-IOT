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
            var poll = Between(source, "static void Motor_Poll", "static void Protocol_Poll");

            Assert.Contains("if (UpperLimit_IsActive())", up);
            Assert.Contains("return 0; // physical upper limit is active", up);
            Assert.Contains("upperLimitActive && currentDir == MOTOR_UP", poll);
            Assert.Contains("static uint8_t Motor_RunDown", source);
            Assert.Contains("physical DOWN", source);
            Assert.Contains("LowerLimit_IsActive()", source);
        }

        private static string Between(string source, string start, string end)
        {
            var startIndex = source.LastIndexOf(start, StringComparison.Ordinal);
            var endIndex = source.LastIndexOf(end, StringComparison.Ordinal);
            Assert.True(startIndex >= 0, "Missing start marker: " + start);
            Assert.True(endIndex > startIndex, "Missing end marker: " + end);
            return source.Substring(startIndex, endIndex - startIndex);
        }
    }
}