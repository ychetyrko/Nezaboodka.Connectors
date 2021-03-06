CREATE TABLE `${db_name}`.`type`(
    `id` INT PRIMARY KEY NOT NULL AUTO_INCREMENT,
    `name` VARCHAR(128) NOT NULL UNIQUE
        CHECK(`name` != ''),
    `table_name` VARCHAR(64) NOT NULL UNIQUE COLLATE `UTF8_GENERAL_CI`
        CHECK(`table_name` != ''),
    `base_type_name` VARCHAR(128) NOT NULL DEFAULT ''
) ENGINE=`INNODB`;
